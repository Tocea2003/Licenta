using Microsoft.AspNetCore.SignalR;
using TursibBackend.Services;

namespace TursibBackend.Hubs;

/// <summary>
/// Canalul live către clienți. La conectare clientul cere <see cref="GetSnapshot"/>, apoi primește
/// loturi de poziții noi prin evenimentul <see cref="PositionsEvent"/>.
/// </summary>
public class BusHub : Hub
{
    public const string PositionsEvent = "positions";

    private readonly LiveBusState _state;
    public BusHub(LiveBusState state) => _state = state;

    public IReadOnlyList<BusPosition> GetSnapshot() => _state.Snapshot();
}
