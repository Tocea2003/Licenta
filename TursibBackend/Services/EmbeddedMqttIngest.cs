using System.Collections.Concurrent;
using TursibMessaging;

namespace TursibBackend.Services;

/// <summary>
/// Ingestia MQTT rulată chiar în backend (<c>Ingestion:EmbeddedMqtt=true</c>): aceeași validare ca serviciul de
/// ingestie (<see cref="PositionParser"/>), dar pozițiile merg direct în <see cref="LiveIngestService"/>, fără Kafka.
/// Pentru implementări mici, unde nu merită un worker și un cluster Kafka în plus (ex. planul gratuit Render:
/// simulator → broker MQTT gestionat → backend). Cu infrastructura completă se folosește serviciul de ingestie + Kafka.
/// </summary>
public class EmbeddedMqttIngest : BackgroundService
{
    private readonly MqttOptions _options;
    private readonly LiveIngestService _ingest;
    private readonly ILogger<EmbeddedMqttIngest> _logger;
    private readonly TimeProvider _time;
    private readonly ConcurrentQueue<BusPosition> _pending = new();
    private long _received, _rejected;

    public EmbeddedMqttIngest(MqttOptions options, LiveIngestService ingest, ILogger<EmbeddedMqttIngest> logger, TimeProvider? time = null)
    {
        _options = options;
        _ingest = ingest;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public long Received => Interlocked.Read(ref _received);
    public long Rejected => Interlocked.Read(ref _rejected);

    /// <summary>Validează un mesaj și îl pune în coada următorului lot; false dacă a fost respins.</summary>
    public bool Handle(string topic, ReadOnlySpan<byte> payload)
    {
        Interlocked.Increment(ref _received);
        if (!PositionParser.TryParse(topic, payload, _time.GetUtcNow(), out var message, out var reason))
        {
            Interlocked.Increment(ref _rejected);
            _logger.LogDebug("Mesaj MQTT respins ({Reason}) pe {Topic}", reason, topic);
            return false;
        }
        _pending.Enqueue(ToBusPosition(message!));
        return true;
    }

    public static BusPosition ToBusPosition(VehiclePositionMessage m) => new()
    {
        Id = m.Id, Latitude = m.Latitude, Longitude = m.Longitude, RouteId = m.RouteId, RouteNumber = m.RouteNumber,
        RouteName = m.RouteName, Timestamp = m.Timestamp, Speed = m.Speed, Heading = m.Heading, Occupancy = m.Occupancy,
        Status = m.Status, DirectionId = m.DirectionId, NextStationId = m.NextStationId,
        NextStationName = m.NextStationName, NextStationEta = m.NextStationEta
    };

    /// <summary>Trimite mesajele strânse ca un singur lot (un eveniment SignalR, nu unul per mesaj).</summary>
    public int Flush()
    {
        var batch = new List<BusPosition>();
        while (batch.Count < LiveBusState.MaxBatchSize && _pending.TryDequeue(out var p)) batch.Add(p);
        if (batch.Count > 0) _ingest.Ingest(batch);
        return batch.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = new MqttSubscriber(_options,
            (topic, payload, _) => { Handle(topic, payload.Span); return Task.CompletedTask; },
            _logger, "tursib-backend");
        await subscriber.StartAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) Flush();
        }
        catch (OperationCanceledException) { }
        await subscriber.StopAsync(CancellationToken.None);
    }
}
