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

    public LiveIngestService(LiveBusState state, LiveEventBus bus)
    {
        _state = state;
        _bus = bus;
    }

    /// <returns>pozițiile acceptate (valide și mai noi decât cele deja cunoscute)</returns>
    public IReadOnlyList<BusPosition> Ingest(IEnumerable<BusPosition> batch)
    {
        var accepted = _state.Apply(batch);
        _bus.Publish(accepted);
        return accepted;
    }
}
