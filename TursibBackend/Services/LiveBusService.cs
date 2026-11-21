using System.Collections.Concurrent;
using System.Diagnostics;
using TursibMessaging;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using TursibBackend.Hubs;

namespace TursibBackend.Services;

/// <summary>Poziția live a unui autobuz (același format ca nodul Firebase <c>bus_locations</c>).</summary>
public record BusPosition
{
    /// <summary>Ex. route_12_bus_0</summary>
    public string Id { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int RouteId { get; init; }
    public string? RouteNumber { get; init; }
    public string? RouteName { get; init; }
    /// <summary>Secunde Unix (UTC). Dacă lipsește, o setează serverul.</summary>
    public long Timestamp { get; init; }
    /// <summary>km/h</summary>
    public double Speed { get; init; }
    public double Heading { get; init; }
    public int? Occupancy { get; init; }
    public string? Status { get; init; }
    public int? DirectionId { get; init; }
    public int? NextStationId { get; init; }
    public string? NextStationName { get; init; }
    /// <summary>secunde</summary>
    public int? NextStationEta { get; init; }
}

/// <summary>Ultima poziție cunoscută a fiecărui autobuz, ținută în memorie (singleton).</summary>
public class LiveBusState
{
    public const int MaxBatchSize = 2000;

    private readonly ConcurrentDictionary<string, BusPosition> _positions = new();
    private readonly TimeProvider _time;

    public LiveBusState(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <summary>
    /// Validează și aplică un lot de poziții. Întoarce doar pozițiile acceptate (cu timestamp completat);
    /// o poziție mai veche decât cea deja cunoscută pentru același autobuz este ignorată.
    /// </summary>
    public IReadOnlyList<BusPosition> Apply(IEnumerable<BusPosition> batch)
    {
        var accepted = new List<BusPosition>();
        foreach (var raw in batch)
        {
            if (!IsValid(raw)) continue;

            var position = raw.Timestamp > 0 ? raw : raw with { Timestamp = _time.GetUtcNow().ToUnixTimeSeconds() };
            var isNewer = true;
            _positions.AddOrUpdate(
                position.Id,
                position,
                (_, existing) =>
                {
                    if (position.Timestamp < existing.Timestamp) { isNewer = false; return existing; }
                    return position;
                });
            if (isNewer) accepted.Add(position);
        }
        return accepted;
    }

    public IReadOnlyList<BusPosition> Snapshot() => _positions.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();

    /// <summary>Elimină autobuzele care nu au mai raportat de cel puțin <paramref name="maxAge"/>.</summary>
    public int RemoveStale(TimeSpan maxAge)
    {
        var cutoff = _time.GetUtcNow().Add(-maxAge).ToUnixTimeSeconds();
        var removed = 0;
        foreach (var (id, position) in _positions)
            if (position.Timestamp < cutoff && _positions.TryRemove(new KeyValuePair<string, BusPosition>(id, position)))
                removed++;
        return removed;
    }

    public static bool IsValid(BusPosition p) =>
        p.Id.StartsWith("route_", StringComparison.Ordinal)
        && p.Id.Length <= 64
        && p.RouteId > 0
        && p.Latitude is >= -90 and <= 90
        && p.Longitude is >= -180 and <= 180
        && !(p.Latitude == 0 && p.Longitude == 0);
}

/// <summary>Un consumator al evenimentelor de poziție (broadcast SignalR, detecție bunching etc.).</summary>
public interface IBusPositionHandler
{
    Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct);
}

/// <summary>Coadă de evenimente: ingest-ul publică, <see cref="LiveEventDispatcher"/> livrează handler-elor.</summary>
/// <summary>Un lot de poziții pe magistrală, cu contextul urmei care l-a produs (ca handler-ele să continue aceeași urmă).</summary>
public record LiveEvent(IReadOnlyList<BusPosition> Positions, ActivityContext Context);

public class LiveEventBus
{
    private readonly Channel<LiveEvent> _channel = Channel.CreateBounded<LiveEvent>(
        new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Publish(IReadOnlyList<BusPosition> positions)
    {
        if (positions.Count > 0) _channel.Writer.TryWrite(new LiveEvent(positions, Activity.Current?.Context ?? default));
    }

    public ChannelReader<LiveEvent> Reader => _channel.Reader;
}

/// <summary>Citește evenimentele din coadă și le trimite fiecărui handler; un handler care cade nu le oprește pe celelalte.</summary>
public class LiveEventDispatcher : BackgroundService
{
    private readonly LiveEventBus _bus;
    private readonly IEnumerable<IBusPositionHandler> _handlers;
    private readonly ILogger<LiveEventDispatcher> _logger;

    public LiveEventDispatcher(LiveEventBus bus, IEnumerable<IBusPositionHandler> handlers, ILogger<LiveEventDispatcher> logger)
    {
        _bus = bus;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var live in _bus.Reader.ReadAllAsync(stoppingToken))
        {
            var batch = live.Positions;
            foreach (var handler in _handlers)
            {
                using var activity = PipelineTelemetry.Source.StartActivity(
                    "handle " + handler.GetType().Name, ActivityKind.Internal, live.Context);
                activity?.SetTag("tursib.positions", batch.Count);
                try { await handler.HandleAsync(batch, stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Handler {Handler} failed", handler.GetType().Name);
                }
            }
        }
    }
}

/// <summary>Trimite fiecare lot de poziții tuturor clienților conectați la <see cref="BusHub"/>.</summary>
public class HubBroadcastHandler : IBusPositionHandler
{
    private readonly IHubContext<BusHub> _hub;
    public HubBroadcastHandler(IHubContext<BusHub> hub) => _hub = hub;

    public Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct) =>
        _hub.Clients.All.SendAsync(BusHub.PositionsEvent, positions, ct);
}

/// <summary>Curăță periodic autobuzele care nu mai raportează (simulatorul oprit => nu rămân „fantome”).</summary>
public class StaleBusCleaner : BackgroundService
{
    private readonly LiveBusState _state;
    public StaleBusCleaner(LiveBusState state) => _state = state;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            _state.RemoveStale(TimeSpan.FromMinutes(5));
    }
}
