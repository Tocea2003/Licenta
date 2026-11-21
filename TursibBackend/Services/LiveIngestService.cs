using System.Diagnostics;
using TursibMessaging;

namespace TursibBackend.Services;

/// <summary>
/// Punctul unic prin care pozițiile intră în platformă, indiferent de transport (Kafka, HTTP): validează și
/// aplică pe <see cref="LiveBusState"/>, apoi publică pozițiile acceptate pe magistrala de evenimente
/// (SignalR, bunching, istoric).
/// </summary>
public class LiveIngestService
{
    private readonly LiveBusState _state;
    private readonly LiveEventBus _bus;
    private long _accepted;
    private long _lastIngestUnix;

    /// <summary>Câte poziții au fost acceptate de la pornire.</summary>
    public long AcceptedTotal => Interlocked.Read(ref _accepted);
    /// <summary>Când a sosit ultima poziție acceptată (secunde Unix), null dacă niciuna.</summary>
    public long? LastIngestAt => Interlocked.Read(ref _lastIngestUnix) is var t && t > 0 ? t : null;

    public LiveIngestService(LiveBusState state, LiveEventBus bus)
    {
        _state = state;
        _bus = bus;
    }

    /// <returns>pozițiile acceptate (valide și mai noi decât cele deja cunoscute)</returns>
    public IReadOnlyList<BusPosition> Ingest(IEnumerable<BusPosition> batch, string transport = "http")
    {
        var positions = batch as IReadOnlyCollection<BusPosition> ?? batch.ToList();
        using var activity = PipelineTelemetry.Source.StartActivity("live ingest");
        activity?.SetTag("tursib.transport", transport);
        activity?.SetTag("tursib.received", positions.Count);
        var stage = new KeyValuePair<string, object?>("stage", "backend");
        PipelineTelemetry.Received.Add(positions.Count, stage, new("transport", transport));

        var accepted = _state.Apply(positions);
        activity?.SetTag("tursib.accepted", accepted.Count);
        PipelineTelemetry.Accepted.Add(accepted.Count, stage);
        var now = DateTimeOffset.UtcNow;
        foreach (var p in accepted) PipelineTelemetry.RecordAge(p.Timestamp, "backend", now);
        if (accepted.Count > 0)
        {
            Interlocked.Add(ref _accepted, accepted.Count);
            Interlocked.Exchange(ref _lastIngestUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        _bus.Publish(accepted);
        return accepted;
    }
}
