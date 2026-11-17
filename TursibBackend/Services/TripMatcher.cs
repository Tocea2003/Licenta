using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;

namespace TursibBackend.Services;

// Potrivirea unui vehicul cu cursa programată (trip_id) din GTFS. Dispozitivele din autobuze raportează doar poziția,
// nu și cursa, așa că operatorii o deduc în backend (map-matching pe orar): printre cursele care circulă azi pe
// aceeași linie și direcție și care trec prin următoarea stație a vehiculului, se alege cea mai apropiată ca oră.

/// <summary>Secțiunea de configurare „TripMatching”.</summary>
public class TripMatchOptions
{
    /// <summary>Un vehicul nu e potrivit cu o cursă al cărei orar în următoarea stație e la mai mult de atât de „acum”.</summary>
    public int MaxDeviationSeconds { get; set; } = 30 * 60;
}

/// <summary>Cursa atribuită unui vehicul, în formatele cerute de GTFS-RT.</summary>
public record TripAssignment(string TripId, string StartTime, string StartDate);

public static class GtfsTime
{
    /// <summary>"HH:MM:SS" (orele pot trece de 24) în secunde; -1 dacă nu se poate citi.</summary>
    public static int Parse(string? value)
    {
        var parts = value?.Trim().Split(':');
        if (parts is not { Length: 3 }
            || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var s))
            return -1;
        return h * 3600 + m * 60 + s;
    }
}

/// <summary>Orarul zilei: cursele active, indexate ca să găsim repede cursele unei linii care trec printr-o stație.</summary>
public sealed class TripScheduleIndex
{
    public sealed record Entry(string TripId, int RouteId, int DirectionId, string StartTime, int[] StopIds, int[] DepartureSeconds);

    private readonly Dictionary<string, Entry> _byTripId = new();
    private readonly Dictionary<(int Route, int Direction, int Stop), List<(Entry Trip, int StopIndex)>> _byStop = new();
    private readonly HashSet<(int Route, int Direction)> _routeDirections = new();

    public static TripScheduleIndex Build(IEnumerable<Entry> trips)
    {
        var index = new TripScheduleIndex();
        foreach (var trip in trips)
        {
            index._byTripId[trip.TripId] = trip;
            index._routeDirections.Add((trip.RouteId, trip.DirectionId));
            for (var i = 0; i < trip.StopIds.Length; i++)
            {
                var key = (trip.RouteId, trip.DirectionId, trip.StopIds[i]);
                if (!index._byStop.TryGetValue(key, out var list)) index._byStop[key] = list = new();
                list.Add((trip, i));
            }
        }
        return index;
    }

    public int TripCount => _byTripId.Count;
    public Entry? Get(string tripId) => _byTripId.GetValueOrDefault(tripId);
    public bool HasDirection(int route, int direction) => _routeDirections.Contains((route, direction));

    public IReadOnlyList<(Entry Trip, int StopIndex)> Through(int route, int direction, int stop) =>
        _byStop.TryGetValue((route, direction, stop), out var list) ? list : Array.Empty<(Entry, int)>();
}

/// <summary>
/// Atribuie vehiculelor curse din orar. Atribuirea e stabilă: un vehicul păstrează cursa cât timp rămâne plauzibilă
/// (aceeași direcție, înaintează pe traseu, abatere de orar în prag), ca <c>trip_id</c> să nu sară de la o cerere la alta,
/// și două vehicule nu primesc aceeași cursă.
/// </summary>
public class TripMatcher
{
    private static readonly TimeSpan MissingIndexRetry = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly TripMatchOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TripMatcher> _logger;
    private readonly TimeZoneInfo _zone = ResolveZone();
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private readonly ConcurrentDictionary<string, (string TripId, int Direction, int StopIndex)> _assigned = new();

    private (DateOnly Date, TripScheduleIndex? Index, DateTimeOffset BuiltAt)? _cache;

    public TripMatcher(IServiceScopeFactory scopes, TripMatchOptions options, ILogger<TripMatcher> logger, TimeProvider? time = null)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Europe/Bucharest", "GTB Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("RO-Fallback", TimeSpan.FromHours(2), "Romania", "Romania");
    }

    /// <returns>vehicul → cursă, doar pentru vehiculele pentru care s-a găsit o potrivire</returns>
    public async Task<IReadOnlyDictionary<string, TripAssignment>> AssignAsync(
        IEnumerable<BusPosition> positions, DateTimeOffset now, CancellationToken ct)
    {
        var local = TimeZoneInfo.ConvertTime(now, _zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        var index = await GetIndexAsync(date, ct);
        var result = new Dictionary<string, TripAssignment>();
        if (index == null) return result;

        var nowSeconds = (int)local.TimeOfDay.TotalSeconds;
        var startDate = date.ToString("yyyyMMdd");
        var vehicles = positions.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var taken = new HashSet<string>();

        // 1) cele care își păstrează cursa
        foreach (var bus in vehicles)
        {
            if (!_assigned.TryGetValue(bus.Id, out var previous)) continue;
            var direction = bus.DirectionId ?? 0;
            var trip = index.Get(previous.TripId);
            if (trip != null && trip.RouteId == bus.RouteId && previous.Direction == direction && bus.NextStationId is { } stop
                && !taken.Contains(trip.TripId)
                && StopIndexAtOrAfter(trip, stop, previous.StopIndex) is { } stopIndex
                && Math.Abs(trip.DepartureSeconds[stopIndex] - nowSeconds) <= _options.MaxDeviationSeconds)
            {
                taken.Add(trip.TripId);
                _assigned[bus.Id] = (trip.TripId, direction, stopIndex);
                result[bus.Id] = new TripAssignment(trip.TripId, trip.StartTime, startDate);
            }
        }

        // 2) restul: cea mai apropiată cursă liberă care trece prin următoarea stație
        foreach (var bus in vehicles.Where(b => !result.ContainsKey(b.Id)))
        {
            _assigned.TryRemove(bus.Id, out _);
            if (bus.NextStationId is not { } stop) continue;

            var direction = bus.DirectionId ?? 0;
            // linie cu o singură direcție în orar (ex. circulară): direcția raportată nu mai contează
            if (!index.HasDirection(bus.RouteId, direction) && index.HasDirection(bus.RouteId, 1 - direction))
                direction = 1 - direction;

            var best = index.Through(bus.RouteId, direction, stop)
                .Where(c => !taken.Contains(c.Trip.TripId))
                .Select(c => (c.Trip, c.StopIndex, Deviation: Math.Abs(c.Trip.DepartureSeconds[c.StopIndex] - nowSeconds)))
                .Where(c => c.Deviation <= _options.MaxDeviationSeconds)
                .OrderBy(c => c.Deviation).ThenBy(c => c.Trip.TripId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best.Trip == null) continue;

            taken.Add(best.Trip.TripId);
            _assigned[bus.Id] = (best.Trip.TripId, direction, best.StopIndex);
            result[bus.Id] = new TripAssignment(best.Trip.TripId, best.Trip.StartTime, startDate);
        }

        // vehiculele care nu mai raportează își pierd atribuirea
        var live = vehicles.Select(v => v.Id).ToHashSet();
        foreach (var id in _assigned.Keys.Where(id => !live.Contains(id))) _assigned.TryRemove(id, out _);
        return result;
    }

    private static int? StopIndexAtOrAfter(TripScheduleIndex.Entry trip, int stop, int from)
    {
        for (var i = Math.Max(0, from); i < trip.StopIds.Length; i++)
            if (trip.StopIds[i] == stop) return i;
        return null;
    }

    private async Task<TripScheduleIndex?> GetIndexAsync(DateOnly date, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        if (_cache is { } c && c.Date == date && (c.Index != null || now - c.BuiltAt < MissingIndexRetry)) return c.Index;

        await _buildLock.WaitAsync(ct);
        try
        {
            if (_cache is { } again && again.Date == date && (again.Index != null || now - again.BuiltAt < MissingIndexRetry))
                return again.Index;

            TripScheduleIndex? index = null;
            try { index = await BuildAsync(date, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Nu am putut construi orarul pentru potrivirea curselor");
            }
            _cache = (date, index, now);
            return index;
        }
        finally { _buildLock.Release(); }
    }

    private async Task<TripScheduleIndex?> BuildAsync(DateOnly date, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<RouteCalculatorService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var active = await planner.GetActiveServiceIdsAsync(date.ToDateTime(TimeOnly.MinValue));
        if (active.Count == 0) return null;

        var rows = await (
            from t in db.Trips.AsNoTracking()
            join st in db.StopTimes.AsNoTracking() on t.TripId equals st.TripId
            where t.ServiceId != null && active.Contains(t.ServiceId)
            orderby t.TripId, st.StopSequence
            select new { t.TripId, t.RouteId, Direction = t.DirectionId ?? 0, st.StopId, st.DepartureTime }
        ).ToListAsync(ct);

        var trips = new List<TripScheduleIndex.Entry>();
        foreach (var group in rows.GroupBy(r => r.TripId))
        {
            var stops = group.Select(r => (r.StopId, Sec: GtfsTime.Parse(r.DepartureTime))).Where(x => x.Sec >= 0).ToList();
            if (stops.Count == 0) continue;
            var first = group.First();
            var start = stops[0].Sec;
            trips.Add(new TripScheduleIndex.Entry(
                first.TripId, first.RouteId, first.Direction, $"{start / 3600:00}:{start % 3600 / 60:00}:{start % 60:00}",
                stops.Select(x => x.StopId).ToArray(), stops.Select(x => x.Sec).ToArray()));
        }
        return TripScheduleIndex.Build(trips);
    }
}
