using System.Collections.Concurrent;
using System.Text.Json;
using TursibMessaging;

namespace TursibIngestion;

/// <summary>Unde ajung mesajele valide (Kafka în producție, o listă în teste).</summary>
public interface IPositionSink
{
    Task PublishAsync(string vehicleId, string json, CancellationToken ct);
    Task PublishRejectedAsync(string topic, ReadOnlyMemory<byte> payload, RejectReason reason, CancellationToken ct);
}

/// <summary>Contoare simple, citite de <see cref="MetricsLogger"/>.</summary>
public class IngestionMetrics
{
    private long _received, _published, _failed;
    private readonly ConcurrentDictionary<RejectReason, long> _rejected = new();

    public long Received => Interlocked.Read(ref _received);
    public long Published => Interlocked.Read(ref _published);
    public long Failed => Interlocked.Read(ref _failed);
    public IReadOnlyDictionary<RejectReason, long> Rejected => _rejected;
    public long RejectedTotal => _rejected.Values.Sum();

    public void OnReceived() => Interlocked.Increment(ref _received);
    public void OnPublished() => Interlocked.Increment(ref _published);
    public void OnFailed() => Interlocked.Increment(ref _failed);
    public void OnRejected(RejectReason reason) => _rejected.AddOrUpdate(reason, 1, (_, n) => n + 1);
}

/// <summary>Un mesaj MQTT intră, e validat și normalizat, apoi merge la sink; cele invalide merg în DLQ cu motivul.</summary>
public class PositionIngestor
{
    private readonly IPositionSink _sink;
    private readonly IngestionMetrics _metrics;
    private readonly TimeProvider _time;
    private readonly ILogger<PositionIngestor> _logger;

    public PositionIngestor(IPositionSink sink, IngestionMetrics metrics, ILogger<PositionIngestor> logger, TimeProvider? time = null)
    {
        _sink = sink;
        _metrics = metrics;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task HandleAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        _metrics.OnReceived();

        if (!PositionParser.TryParse(topic, payload.Span, _time.GetUtcNow(), out var message, out var reason))
        {
            _metrics.OnRejected(reason);
            _logger.LogDebug("Mesaj respins ({Reason}) pe {Topic}", reason, topic);
            try { await _sink.PublishRejectedAsync(topic, payload, reason, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogWarning(ex, "Nu am putut trimite mesajul respins în DLQ"); }
            return;
        }

        try
        {
            await _sink.PublishAsync(message!.Id, JsonSerializer.Serialize(message, VehiclePositionMessage.Json), ct);
            _metrics.OnPublished();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.OnFailed();
            _logger.LogWarning(ex, "Publicarea pe Kafka a eșuat pentru {Vehicle}", message!.Id);
        }
    }
}

/// <summary>Loghează periodic contoarele (volum, respinse, eșecuri).</summary>
public class MetricsLogger : BackgroundService
{
    private readonly IngestionMetrics _metrics;
    private readonly ILogger<MetricsLogger> _logger;

    public MetricsLogger(IngestionMetrics metrics, ILogger<MetricsLogger> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var rejected = string.Join(", ", _metrics.Rejected.Select(kv => $"{kv.Key}={kv.Value}"));
            _logger.LogInformation("Ingestie: primite={Received} publicate={Published} respinse={Rejected} ({Reasons}) eșuate={Failed}",
                _metrics.Received, _metrics.Published, _metrics.RejectedTotal, rejected.Length == 0 ? "-" : rejected, _metrics.Failed);
        }
    }
}
