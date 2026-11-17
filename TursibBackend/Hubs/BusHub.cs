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
    private readonly BunchingDetector _bunching;

    public BusHub(LiveBusState state, BunchingDetector bunching)
    {
        _state = state;
        _bunching = bunching;
    }

    public IReadOnlyList<BusPosition> GetSnapshot() => _state.Snapshot();

    /// <summary>Alertele de bunching active acum (apoi clientul primește evenimentul „bunching” la fiecare schimbare).</summary>
    public IReadOnlyList<BunchingAlert> GetBunching() => _bunching.Active;
}
