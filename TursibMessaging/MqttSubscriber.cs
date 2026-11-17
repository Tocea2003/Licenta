using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace TursibMessaging;

public class MqttOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    /// <summary>TLS (portul uzual 8883). Brokerele gestionate (HiveMQ Cloud, EMQX Cloud) îl cer.</summary>
    public bool UseTls { get; set; }
    /// <summary>Certificat CA (PEM) pentru un broker cu certificat propriu; altfel se folosește trust store-ul sistemului.</summary>
    public string? CaCertificatePath { get; set; }
    /// <summary>Un dispozitiv publică pe tursib/vehicles/{id}/position.</summary>
    public string TopicFilter { get; set; } = "tursib/vehicles/+/position";
    /// <summary>
    /// Dacă e setat, ne abonăm la <c>$share/{grup}/{filtru}</c>: mai multe instanțe își împart mesajele
    /// (scalare orizontală; necesită broker MQTT 5 / Mosquitto 2.x cu shared subscriptions).
    /// </summary>
    public string? SharedGroup { get; set; }

    public MqttClientOptions Build(string clientIdPrefix)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(Host, Port)
            .WithClientId(ClientId(clientIdPrefix))
            .WithCleanSession();
        if (!string.IsNullOrEmpty(Username))
            builder.WithCredentials(Username, Password);
        if (UseTls)
        {
            builder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                if (!string.IsNullOrEmpty(CaCertificatePath))
                {
                    var chain = new X509Certificate2Collection();
                    chain.ImportFromPemFile(CaCertificatePath);
                    tls.WithTrustChain(chain);
                }
            });
        }
        return builder.Build();
    }

    private static string ClientId(string prefix)
    {
        var id = $"{prefix}-{Guid.NewGuid():N}";
        return id[..Math.Min(id.Length, 40)];
    }

    public string SubscriptionFilter => string.IsNullOrEmpty(SharedGroup) ? TopicFilter : $"$share/{SharedGroup}/{TopicFilter}";
}

/// <summary>Primește un mesaj MQTT: topicul și payload-ul brut.</summary>
public delegate Task MqttMessageHandler(string topic, ReadOnlyMemory<byte> payload, CancellationToken ct);

/// <summary>
/// Se abonează la brokerul MQTT, își revine singur după pierderea conexiunii (reîncearcă la 5 s și se reabonează)
/// și trimite mesajele la <see cref="MqttMessageHandler"/>. Folosit atât de serviciul de ingestie, cât și de
/// ingestia integrată în backend.
/// </summary>
public class MqttSubscriber : BackgroundService
{
    private readonly MqttOptions _options;
    private readonly MqttMessageHandler _handler;
    private readonly ILogger _logger;
    private readonly string _clientIdPrefix;
    private IMqttClient? _client;

    /// <summary>Dacă suntem conectați acum la broker (pentru pagina de stare).</summary>
    public bool IsConnected => _client?.IsConnected ?? false;

    public MqttSubscriber(MqttOptions options, MqttMessageHandler handler, ILogger logger, string clientIdPrefix = "tursib")
    {
        _options = options;
        _handler = handler;
        _logger = logger;
        _clientIdPrefix = clientIdPrefix;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        _client = client;

        client.ApplicationMessageReceivedAsync += e =>
            _handler(e.ApplicationMessage.Topic, e.ApplicationMessage.PayloadSegment.ToArray(), stoppingToken);

        var connectOptions = _options.Build(_clientIdPrefix);
        var filter = _options.SubscriptionFilter;
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
                    _logger.LogInformation("Conectat la MQTT {Host}:{Port}{Tls}, abonat la {Filter}",
                        _options.Host, _options.Port, _options.UseTls ? " (TLS)" : "", filter);
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
        _client = null;
    }
}
