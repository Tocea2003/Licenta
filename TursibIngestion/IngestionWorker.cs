using System.Text;
using Confluent.Kafka;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using Tursib.Contracts;

namespace TursibIngestion;

public sealed class IngestionOptions
{
    public string MqttHost { get; set; } = "localhost";
    public int MqttPort { get; set; } = 1883;
    public string KafkaBootstrapServers { get; set; } = "localhost:19092";
}

/// <summary>
/// MQTT → validation → Kafka. Valid positions go to "vehicle-positions" keyed by
/// vehicle id (so each bus stays ordered within one partition); rejected messages
/// go to the dead-letter topic with the reason in a header.
/// </summary>
public sealed class IngestionWorker : BackgroundService
{
    private readonly IngestionOptions options;
    private readonly IngestionStats stats;
    private readonly ILogger<IngestionWorker> logger;

    public IngestionWorker(IngestionOptions options, IngestionStats stats, ILogger<IngestionWorker> logger)
    {
        this.options = options;
        this.stats = stats;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers = options.KafkaBootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            LingerMs = 20,
            CompressionType = CompressionType.Lz4,
            ClientId = "tursib-ingestion"
        })
        .SetErrorHandler((_, e) => logger.LogWarning("Kafka: {Reason}", e.Reason))
        .Build();

        using var mqtt = new MqttFactory().CreateManagedMqttClient();
        mqtt.ConnectedAsync += _ =>
        {
            stats.MqttConnected = true;
            logger.LogInformation("MQTT connected to {Host}:{Port}", options.MqttHost, options.MqttPort);
            return Task.CompletedTask;
        };
        mqtt.DisconnectedAsync += e =>
        {
            stats.MqttConnected = false;
            if (e.ClientWasConnected)
                logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
            return Task.CompletedTask;
        };
        mqtt.ApplicationMessageReceivedAsync += e =>
        {
            Handle(producer, e.ApplicationMessage.Topic, e.ApplicationMessage.PayloadSegment);
            return Task.CompletedTask;
        };

        await mqtt.SubscribeAsync(Topics.MqttPositionFilter, MqttQualityOfServiceLevel.AtLeastOnce);
        await mqtt.StartAsync(new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithTcpServer(options.MqttHost, options.MqttPort)
                .WithClientId($"tursib-ingestion-{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build())
            .Build());

        logger.LogInformation("Ingesting {Filter} → Kafka {Servers}", Topics.MqttPositionFilter, options.KafkaBootstrapServers);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                logger.LogInformation(
                    "received={Received} accepted={Accepted} rejected={Rejected} produceErrors={Errors}",
                    stats.Received, stats.Accepted, stats.Rejected, stats.ProduceErrors);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        finally
        {
            await mqtt.StopAsync();
            producer.Flush(TimeSpan.FromSeconds(5));
        }
    }

    private void Handle(IProducer<string, byte[]> producer, string topic, ArraySegment<byte> payload)
    {
        stats.OnReceived();
        var result = PositionValidator.Validate(topic, payload, DateTimeOffset.UtcNow);

        if (result.IsValid)
        {
            stats.OnAccepted();
            Produce(producer, Topics.KafkaVehiclePositions, result.Position!.VehicleId, payload.ToArray(), null);
        }
        else
        {
            stats.OnRejected(result.Reason!);
            var headers = new Headers
            {
                { "reason", Encoding.UTF8.GetBytes(result.Reason!) },
                { "mqtt-topic", Encoding.UTF8.GetBytes(topic) }
            };
            Produce(producer, Topics.KafkaVehiclePositionsDlq, result.Position?.VehicleId ?? topic, payload.ToArray(), headers);
        }
    }

    private void Produce(IProducer<string, byte[]> producer, string topic, string key, byte[] value, Headers? headers)
    {
        try
        {
            producer.Produce(topic, new Message<string, byte[]> { Key = key, Value = value, Headers = headers }, report =>
            {
                if (report.Error.IsError)
                {
                    stats.OnProduceError();
                    logger.LogWarning("Produce to {Topic} failed: {Reason}", topic, report.Error.Reason);
                }
            });
        }
        catch (ProduceException<string, byte[]> ex)
        {
            stats.OnProduceError();
            logger.LogWarning("Produce to {Topic} failed: {Reason}", topic, ex.Error.Reason);
        }
    }
}
