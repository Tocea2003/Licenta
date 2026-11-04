using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TursibBackend.Hubs;

namespace TursibBackend.Services.Events;

/// <summary>
/// Pushes changed positions to every map client in batches (default every 500 ms)
/// and tells clients which vehicles disappeared.
/// </summary>
public sealed class LiveBroadcastService : BackgroundService
{
    private static readonly TimeSpan PruneEvery = TimeSpan.FromSeconds(30);

    private readonly EventPipelineOptions options;
    private readonly LiveVehicleState state;
    private readonly IHubContext<VehiclesHub> hub;
    private readonly ILogger<LiveBroadcastService> logger;

    public LiveBroadcastService(
        IOptions<EventPipelineOptions> options,
        LiveVehicleState state,
        IHubContext<VehiclesHub> hub,
        ILogger<LiveBroadcastService> logger)
    {
        this.options = options.Value;
        this.state = state;
        this.hub = hub;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.BroadcastIntervalMs));
        var lastPrune = DateTimeOffset.UtcNow;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var changed = state.DrainChanged();
                if (changed.Count > 0)
                    await hub.Clients.All.SendAsync(VehiclesHub.PositionsMessage, changed, stoppingToken);

                var now = DateTimeOffset.UtcNow;
                if (now - lastPrune >= PruneEvery)
                {
                    lastPrune = now;
                    var removed = state.Prune(now, TimeSpan.FromSeconds(options.StaleAfterSeconds));
                    if (removed.Count > 0)
                        await hub.Clients.All.SendAsync(VehiclesHub.RemovedMessage, removed, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Live broadcast failed");
            }
        }
    }
}
