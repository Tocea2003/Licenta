using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using TursibMessaging;

namespace TursibIngestion;

public class KafkaOptions : KafkaConnectionOptions
{
    public string Topic { get; set; } = "vehicle-positions";
    public string DeadLetterTopic { get; set; } = "vehicle-positions.dlq";
    public int Partitions { get; set; } = 3;
    /// <summary>Pozițiile live nu trebuie păstrate mult în Kafka: istoricul pentru replay e în PostgreSQL.</summary>
    public long RetentionMs { get; set; } = 3_600_000;
}

/// <summary>Publică pe Kafka cu cheia = id-ul vehiculului, ca pozițiile aceluiași autobuz să rămână în ordine (aceeași partiție).</summary>
public sealed class KafkaPositionSink : IPositionSink, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly IProducer<string, byte[]> _dlqProducer;
    private readonly KafkaOptions _options;

    public KafkaPositionSink(KafkaOptions options)
    {
        _options = options;
        var config = new ProducerConfig
        {
            ClientId = "tursib-ingestion",
            EnableIdempotence = true,       // fără dubluri la retrimiteri, ordine păstrată pe partiție
            LingerMs = 20,                  // grupează mesajele apropiate în același lot
            CompressionType = CompressionType.Lz4
        };
        options.Apply(config);                 // adresa brokerului + securitatea (SASL/SSL) din configurare
        _producer = new ProducerBuilder<string, string>(config).Build();
        _dlqProducer = new ProducerBuilder<string, byte[]>(config).Build();
    }

    /// <summary>Creează topicurile dacă lipsesc (idempotent), ca mediul local să pornească fără pași manuali.</summary>
    public async Task EnsureTopicsAsync(ILogger logger, CancellationToken ct)
    {
        var adminConfig = new AdminClientConfig();
        _options.Apply(adminConfig);
        using var admin = new AdminClientBuilder(adminConfig).Build();
        var retention = new Dictionary<string, string> { ["retention.ms"] = _options.RetentionMs.ToString() };
        try
        {
            await admin.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = _options.Topic, NumPartitions = _options.Partitions, ReplicationFactor = 1, Configs = retention },
                new TopicSpecification { Name = _options.DeadLetterTopic, NumPartitions = 1, ReplicationFactor = 1, Configs = retention }
            });
            logger.LogInformation("Topicuri create: {Topic}, {Dlq}", _options.Topic, _options.DeadLetterTopic);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError))
        {
            logger.LogInformation("Topicurile există deja");
        }
    }

    public async Task PublishAsync(string vehicleId, string json, CancellationToken ct)
    {
        using var activity = PipelineTelemetry.Source.StartActivity("kafka produce " + _options.Topic, ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", _options.Topic);
        activity?.SetTag("messaging.kafka.message.key", vehicleId);
        var message = new Message<string, string> { Key = vehicleId, Value = json, Headers = new Headers() };
        // contextul urmei trece la backend prin antetul traceparent
        PipelineTelemetry.Inject(activity, (k, v) => message.Headers.Add(k, Encoding.UTF8.GetBytes(v)));
        await _producer.ProduceAsync(_options.Topic, message, ct);
    }

    public async Task PublishRejectedAsync(string topic, ReadOnlyMemory<byte> payload, RejectReason reason, CancellationToken ct) =>
        await _dlqProducer.ProduceAsync(_options.DeadLetterTopic, new Message<string, byte[]>
        {
            Key = topic,
            Value = payload.ToArray(),
            Headers = new Headers
            {
                { "reason", Encoding.UTF8.GetBytes(reason.ToString()) },
                { "mqtt-topic", Encoding.UTF8.GetBytes(topic) }
            }
        }, ct);

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        _dlqProducer.Flush(TimeSpan.FromSeconds(5));
        _dlqProducer.Dispose();
    }
}
