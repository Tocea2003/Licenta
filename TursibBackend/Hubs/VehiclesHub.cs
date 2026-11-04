using Microsoft.AspNetCore.SignalR;
using TursibBackend.Services.Events;

namespace TursibBackend.Hubs;

/// <summary>
/// Live bus positions for the map. Server → client messages:
/// "snapshot" (all vehicles, on connect), "positions" (changed vehicles, batched),
/// "removed" (ids of vehicles that stopped reporting).
/// </summary>
public sealed class VehiclesHub : Hub
{
    public const string Path = "/hubs/vehicles";
    public const string SnapshotMessage = "snapshot";
    public const string PositionsMessage = "positions";
    public const string RemovedMessage = "removed";

    private readonly LiveVehicleState state;

    public VehiclesHub(LiveVehicleState state) => this.state = state;

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(SnapshotMessage, state.Snapshot());
        await base.OnConnectedAsync();
    }
}
