using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using TursibMessaging;

namespace TursibBackend.Services;

/// <summary>Secțiunea de configurare „Kafka”. Fără <see cref="BootstrapServers"/> consumer-ul e oprit (rămâne ingestul HTTP).</summary>
public class KafkaOptions : KafkaConnectionOptions
{
    public string Topic { get; set; } = "vehicle-positions";
    public string GroupId { get; set; } = "tursib-backend";
    public int MaxBatchSize { get; set; } = 500;
    public int MaxBatchWaitMs { get; set; } = 100;
}

public static class KafkaMessageParser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Mesajul de pe Kafka (JSON scris de serviciul de ingestie) ca poziție; false dacă nu e un JSON valid.</summary>
    public static bool TryParse(string? json, out BusPosition? position)
    {
        position = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { position = JsonSerializer.Deserialize<BusPosition>(json, Json); }
        catch (JsonException) { return false; }
        return position != null;
    }
}

/// <summary>
/// Consumă topicul <c>vehicle-positions</c> și trimite loturi la <see cref="LiveIngestService"/>.
/// Livrare cel puțin o dată: offset-ul se comite abia după ce lotul a fost aplicat; o poziție reprocesată e inofensivă,
/// fiindcă <see cref="LiveBusState"/> ignoră pozițiile mai vechi sau egale cu cele cunoscute.
/// </summary>
public class KafkaPositionConsumer : BackgroundService
{
    private readonly KafkaOptions _options;
    private readonly LiveIngestService _ingest;
    private readonly ILogger<KafkaPositionConsumer> _logger;
    private long _lastMessageUnix;

    /// <summary>Când a sosit ultimul mesaj de pe Kafka (secunde Unix), null dacă niciunul.</summary>
    public long? LastMessageAt => Interlocked.Read(ref _lastMessageUnix) is var t && t > 0 ? t : null;

    public KafkaPositionConsumer(KafkaOptions options, LiveIngestService ingest, ILogger<KafkaPositionConsumer> logger)
    {
        _options = options;
        _ingest = ingest;
        _logger = logger;
    }

    // Consume() blochează, deci rulăm bucla pe un fir propriu în loc să ținem un fir din pool ocupat.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => Run(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void Run(CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            GroupId = _options.GroupId,
            ClientId = $"tursib-backend-{Environment.MachineName}",
            EnableAutoCommit = false,
            // Date live: un grup nou pornește de la „acum”; istoricul pentru replay e în PostgreSQL, nu în Kafka.
            AutoOffsetReset = AutoOffsetReset.Latest
        };
        _options.Apply(config);                // adresa brokerului + securitatea (SASL/SSL) din configurare

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka: {Reason}", e.Reason))
            .Build();
        consumer.Subscribe(_options.Topic);
        _logger.LogInformation("Consum {Topic} de pe {Servers} (grup {Group})", _options.Topic, _options.BootstrapServers, _options.GroupId);

        var batch = new List<BusPosition>(_options.MaxBatchSize);
        var links = new List<ActivityLink>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                batch.Clear();
                links.Clear();
                var deadline = DateTime.UtcNow.AddMilliseconds(_options.MaxBatchWaitMs);
                var any = false;

                while (batch.Count < _options.MaxBatchSize)
                {
                    var wait = any ? deadline - DateTime.UtcNow : TimeSpan.FromMilliseconds(500);
                    if (wait <= TimeSpan.Zero) break;

                    ConsumeResult<string, string>? result;
                    try { result = consumer.Consume(wait); }
                    catch (ConsumeException ex)
                    {
                        _logger.LogWarning("Eroare la consum: {Reason}", ex.Error.Reason);
                        break;
                    }
                    if (result?.Message == null) break;

                    any = true;
                    Interlocked.Exchange(ref _lastMessageUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    var parent = PipelineTelemetry.Extract(key =>
                        result.Message.Headers?.TryGetLastBytes(key, out var bytes) == true ? Encoding.UTF8.GetString(bytes) : null);
                    if (parent != default && links.Count < 64) links.Add(new ActivityLink(parent));
                    if (KafkaMessageParser.TryParse(result.Message.Value, out var position)) batch.Add(position!);
                    else _logger.LogDebug("Mesaj Kafka ignorat (JSON invalid) la {Offset}", result.TopicPartitionOffset);
                }

                if (!any) continue;
                // un lot are mai mulți „părinți” (câte unul per mesaj): urma lotului îi leagă prin link-uri
                using (var activity = PipelineTelemetry.Source.StartActivity(
                           "kafka consume " + _options.Topic, ActivityKind.Consumer, default(ActivityContext), links: links))
                {
                    activity?.SetTag("messaging.system", "kafka");
                    activity?.SetTag("messaging.batch.message_count", batch.Count);
                    if (batch.Count > 0) _ingest.Ingest(batch, "kafka");
                }
                try { consumer.Commit(); }
                catch (KafkaException ex) { _logger.LogWarning("Commit eșuat: {Reason}", ex.Error.Reason); }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try { consumer.Close(); } catch (KafkaException) { }
        }
    }
}
