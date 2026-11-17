using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace TursibIngestion;

public class MqttOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    /// <summary>Un dispozitiv publică pe tursib/vehicles/{id}/position.</summary>
    public string TopicFilter { get; set; } = "tursib/vehicles/+/position";
    /// <summary>
    /// Dacă e setat, ne abonăm la <c>$share/{grup}/{filtru}</c>: mai multe instanțe de ingestie își împart mesajele
    /// (scalare orizontală; necesită broker MQTT 5 / Mosquitto 2.0.x care suportă shared subscriptions).
    /// </summary>
    public string? SharedGroup { get; set; }
}

/// <summary>Se abonează la brokerul MQTT, își revine singur după pierderea conexiunii și trimite mesajele la <see cref="PositionIngestor"/>.</summary>
public class MqttSubscriber : BackgroundService
{
    private readonly MqttOptions _options;
    private readonly PositionIngestor _ingestor;
    private readonly ILogger<MqttSubscriber> _logger;

    public MqttSubscriber(MqttOptions options, PositionIngestor ingestor, ILogger<MqttSubscriber> logger)
    {
        _options = options;
        _ingestor = ingestor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += e =>
            _ingestor.HandleAsync(e.ApplicationMessage.Topic, e.ApplicationMessage.PayloadSegment.ToArray(), stoppingToken);

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId($"tursib-ingestion-{Environment.MachineName}-{Guid.NewGuid():N}"[..48])
            .WithCleanSession();
        if (!string.IsNullOrEmpty(_options.Username))
            builder.WithCredentials(_options.Username, _options.Password);
        var connectOptions = builder.Build();

        var filter = string.IsNullOrEmpty(_options.SharedGroup)
            ? _options.TopicFilter
            : $"$share/{_options.SharedGroup}/{_options.TopicFilter}";
        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(filter).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!client.IsConnected)
            {
                try
                {
                    await client.ConnectAsync(connectOptions, stoppingToken);
                    await client.SubscribeAsync(subscribeOptions, stoppingToken);
                    _logger.LogInformation("Conectat la MQTT {Host}:{Port}, abonat la {Filter}", _options.Host, _options.Port, filter);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("MQTT indisponibil ({Message}); reîncerc în 5 s", ex.Message);
                }
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        if (client.IsConnected) await client.DisconnectAsync();
    }
}
