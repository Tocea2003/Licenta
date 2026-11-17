using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Hubs;

namespace TursibBackend.Services;

// Detecția de bunching (autobuze de pe aceeași linie care circulă „lipite”):
//   poziție GPS -> poziție pe traseu (RouteGeometry) -> poziție pe bucla dus-întors -> goluri între
//   autobuze consecutive (BunchingAnalyzer) -> alerte cu histerezis (BunchingTracker) -> SignalR.

/// <summary>Pragurile detectorului (secțiunea de configurare „Bunching”).</summary>
public class BunchingOptions
{
    /// <summary>Alertă când golul față de autobuzul din față scade sub această fracțiune din golul ideal.</summary>
    public double EnterRatio { get; set; } = 0.35;
    /// <summary>Alerta se închide abia când raportul urcă peste această valoare (histerezis).</summary>
    public double ExitRatio { get; set; } = 0.6;
    /// <summary>Cât timp trebuie să persiste situația înainte să devină alertă (ignoră staționările scurte).</summary>
    public int MinDurationSeconds { get; set; } = 10;
    public int EvaluationIntervalSeconds { get; set; } = 2;
    /// <summary>Pozițiile mai vechi de atât nu intră în analiză.</summary>
    public int MaxPositionAgeSeconds { get; set; } = 60;
    /// <summary>Un autobuz mai departe de atât de traseu e considerat în afara traseului și ignorat.</summary>
    public double MaxOffsetMeters { get; set; } = 150;
}

/// <summary>Geometria unui traseu ca polilinie, cu distanța cumulată, pentru a proiecta poziții GPS pe ea.</summary>
public sealed class RouteGeometry
{
    private readonly double[] _lat, _lon, _cum;
    public double LengthMeters => _cum[^1];

    private RouteGeometry(double[] lat, double[] lon, double[] cum) { _lat = lat; _lon = lon; _cum = cum; }

    public static RouteGeometry? FromPoints(IReadOnlyList<(double Lat, double Lon)> points)
    {
        if (points.Count < 2) return null;
        var lat = points.Select(p => p.Lat).ToArray();
        var lon = points.Select(p => p.Lon).ToArray();
        var cum = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            cum[i] = cum[i - 1] + HaversineMeters(lat[i - 1], lon[i - 1], lat[i], lon[i]);
        return cum[^1] < 100 ? null : new RouteGeometry(lat, lon, cum);
    }

    /// <summary>
    /// Cea mai apropiată poziție de pe traseu: distanța parcursă (m) și abaterea laterală (m).
    /// Cu <paramref name="hintAlong"/> (poziția anterioară a aceluiași autobuz) căutăm întâi în apropierea ei,
    /// ca să nu sărim pe alt capăt al unui traseu care se apropie de el însuși (bucle).
    /// </summary>
    public (double Along, double Offset) Project(double lat, double lon, double? hintAlong = null, double windowMeters = 400)
    {
        var mx = 111_320 * Math.Cos(lat * Math.PI / 180);
        const double my = 110_574;

        (double Along, double Offset) best = (0, double.MaxValue), bestNearHint = (0, double.MaxValue);
        for (var i = 0; i < _lat.Length - 1; i++)
        {
            var dx = (_lon[i + 1] - _lon[i]) * mx;
            var dy = (_lat[i + 1] - _lat[i]) * my;
            var px = (lon - _lon[i]) * mx;
            var py = (lat - _lat[i]) * my;
            var len2 = dx * dx + dy * dy;
            var t = len2 < 1e-9 ? 0 : Math.Clamp((px * dx + py * dy) / len2, 0, 1);
            var offset = Math.Sqrt(Math.Pow(px - t * dx, 2) + Math.Pow(py - t * dy, 2));
            var along = _cum[i] + t * (_cum[i + 1] - _cum[i]);

            if (offset < best.Offset) best = (along, offset);
            if (hintAlong is { } hint && Math.Abs(along - hint) <= windowMeters && offset < bestNearHint.Offset)
                bestNearHint = (along, offset);
        }
        return bestNearHint.Offset <= 40 ? bestNearHint : best;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public interface IRouteGeometryProvider
{
    Task<RouteGeometry?> GetAsync(int routeId, CancellationToken ct);
}

/// <summary>Geometria traseului din baza de date: forma primei curse a liniei (aceeași pe care o parcurge simulatorul), cache în memorie.</summary>
public class DbRouteGeometryProvider : IRouteGeometryProvider
{
    private static readonly TimeSpan FoundTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan MissingTtl = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<int, (RouteGeometry? Geometry, DateTimeOffset LoadedAt)> _cache = new();

    public DbRouteGeometryProvider(IServiceScopeFactory scopes, TimeProvider? time = null)
    {
        _scopes = scopes;
        _time = time ?? TimeProvider.System;
    }

    public async Task<RouteGeometry?> GetAsync(int routeId, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        if (_cache.TryGetValue(routeId, out var cached) &&
            now - cached.LoadedAt < (cached.Geometry == null ? MissingTtl : FoundTtl))
            return cached.Geometry;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shapeId = await db.Trips.AsNoTracking()
            .Where(t => t.RouteId == routeId)
            .OrderBy(t => t.Id)
            .Select(t => t.ShapeId)
            .FirstOrDefaultAsync(ct);

        RouteGeometry? geometry = null;
        if (!string.IsNullOrEmpty(shapeId))
        {
            var points = await db.ShapePoints.AsNoTracking()
                .Where(p => p.ShapeId == shapeId)
                .OrderBy(p => p.Sequence)
                .Select(p => new { p.Latitude, p.Longitude })
                .ToListAsync(ct);
            geometry = RouteGeometry.FromPoints(points.Select(p => (p.Latitude, p.Longitude)).ToList());
        }

        _cache[routeId] = (geometry, now);
        return geometry;
    }
}

/// <summary>Un autobuz așezat pe bucla dus-întors a liniei (0..2L, în sensul de mers).</summary>
public record BusOnLoop(string Id, double LoopPosition, double Speed);

/// <summary>Golul dintre un autobuz și cel din fața lui, față de golul ideal (bucla / numărul de autobuze).</summary>
public record BunchingPair(
    int RouteId, string? RouteNumber, string LeaderId, string FollowerId,
    double GapMeters, double IdealGapMeters, double Ratio, double? HeadwaySeconds,
    /// <summary>Cât ar trebui reținut urmăritorul la următoarea stație ca să redea golul (HoldPolicy); null dacă nu se calculează.</summary>
    double? RecommendedHoldSeconds = null);

public static class BunchingAnalyzer
{
    /// <summary>Poziția pe bucla dus-întors: dus = distanța parcursă, întors = 2L - distanța (crește în sensul de mers).</summary>
    public static double LoopPosition(double along, int directionId, double routeLength) =>
        directionId == 1 ? 2 * routeLength - along : along;

    /// <summary>Golurile dintre autobuzele consecutive ale unei linii, circular (ultimul → primul trece prin capătul buclei).</summary>
    public static IReadOnlyList<BunchingPair> ConsecutiveGaps(
        int routeId, string? routeNumber, IReadOnlyList<BusOnLoop> buses, double loopLength, HoldOptions? hold = null)
    {
        if (buses.Count < 2) return Array.Empty<BunchingPair>();

        var ordered = buses.OrderBy(b => b.LoopPosition).ToList();
        var ideal = loopLength / ordered.Count;
        var pairs = new List<BunchingPair>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var follower = ordered[i];
            var leader = ordered[(i + 1) % ordered.Count];
            var gap = leader.LoopPosition - follower.LoopPosition;
            if (gap < 0) gap += loopLength;

            // timpul până ajunge urmăritorul unde e liderul acum, la viteza lui (minim 4 m/s ≈ 15 km/h)
            var speedMs = Math.Max(follower.Speed / 3.6, 4);
            double? recommended = hold == null ? null : HoldPolicy.Recommend(gap, ideal, speedMs, hold);
            pairs.Add(new BunchingPair(routeId, routeNumber, leader.Id, follower.Id, gap, ideal, gap / ideal, gap / speedMs, recommended));
        }
        return pairs;
    }
}

/// <summary>O alertă de bunching (activă sau încheiată). <see cref="EndedAt"/> e null cât timp e activă.</summary>
public record BunchingAlert(
    string Key, int RouteId, string? RouteNumber, string LeaderId, string FollowerId,
    double GapMeters, double IdealGapMeters, double Ratio, string Severity,
    long StartedAt, long? EndedAt, double? HeadwaySeconds, double? RecommendedHoldSeconds = null);

public record BunchingChange(BunchingAlert Alert, bool Started);

/// <summary>Transformă golurile măsurate în alerte stabile: persistență minimă la intrare, histerezis la ieșire.</summary>
public class BunchingTracker
{
    private const int MaxRecent = 50;

    private sealed class Tracked
    {
        public DateTimeOffset BelowSince;
        public DateTimeOffset? StartedAt;
        public BunchingPair Pair = null!;
        public bool Active => StartedAt != null;
    }

    private readonly BunchingOptions _options;
    private readonly Dictionary<string, Tracked> _tracked = new();
    private readonly List<BunchingAlert> _recent = new();

    public BunchingTracker(BunchingOptions options) => _options = options;

    public static string SeverityFor(double ratio) => ratio < 0.15 ? "critical" : "warning";

    public IReadOnlyList<BunchingAlert> Active =>
        _tracked.Where(kv => kv.Value.Active).Select(kv => ToAlert(kv.Key, kv.Value, null))
            .OrderBy(a => a.Ratio).ToList();

    /// <summary>Cele mai recente alerte încheiate, cele mai noi primele.</summary>
    public IReadOnlyList<BunchingAlert> Recent => _recent.AsEnumerable().Reverse().ToList();

    public IReadOnlyList<BunchingChange> Update(IReadOnlyList<BunchingPair> pairs, DateTimeOffset now)
    {
        var changes = new List<BunchingChange>();
        var seen = new HashSet<string>();

        foreach (var pair in pairs)
        {
            var key = $"{pair.RouteId}:{pair.FollowerId}";
            seen.Add(key);
            _tracked.TryGetValue(key, out var tracked);

            if (tracked is { Active: true })
            {
                tracked.Pair = pair;
                if (pair.Ratio > _options.ExitRatio) End(key, tracked, now, changes);
            }
            else if (pair.Ratio < _options.EnterRatio)
            {
                if (tracked == null) _tracked[key] = tracked = new Tracked { BelowSince = now };
                tracked.Pair = pair;
                if (now - tracked.BelowSince >= TimeSpan.FromSeconds(_options.MinDurationSeconds))
                {
                    tracked.StartedAt = now;
                    changes.Add(new BunchingChange(ToAlert(key, tracked, null), true));
                }
            }
            else
            {
                _tracked.Remove(key);
            }
        }

        // perechi care nu mai există (autobuz oprit, ieșit din traseu): le închidem
        foreach (var key in _tracked.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            var tracked = _tracked[key];
            if (tracked.Active) End(key, tracked, now, changes);
            else _tracked.Remove(key);
        }
        return changes;
    }

    private void End(string key, Tracked tracked, DateTimeOffset now, List<BunchingChange> changes)
    {
        var ended = ToAlert(key, tracked, now.ToUnixTimeSeconds());
        _tracked.Remove(key);
        _recent.Add(ended);
        if (_recent.Count > MaxRecent) _recent.RemoveAt(0);
        changes.Add(new BunchingChange(ended, false));
    }

    private static BunchingAlert ToAlert(string key, Tracked t, long? endedAt) => new(
        key, t.Pair.RouteId, t.Pair.RouteNumber, t.Pair.LeaderId, t.Pair.FollowerId,
        Math.Round(t.Pair.GapMeters), Math.Round(t.Pair.IdealGapMeters), Math.Round(t.Pair.Ratio, 2),
        SeverityFor(t.Pair.Ratio), t.StartedAt?.ToUnixTimeSeconds() ?? 0, endedAt,
        t.Pair.HeadwaySeconds is { } h ? Math.Round(h) : null,
        endedAt == null && t.Pair.RecommendedHoldSeconds is > 0 ? Math.Round(t.Pair.RecommendedHoldSeconds.Value) : null);
}

/// <summary>Handler de evenimente: la fiecare lot (cel mult o dată la câteva secunde) analizează flota și publică alertele.</summary>
public class BunchingDetector : IBusPositionHandler
{
    public const string BunchingEvent = "bunching";

    private readonly LiveBusState _state;
    private readonly IRouteGeometryProvider _geometry;
    private readonly IHubContext<BusHub> _hub;
    private readonly BunchingOptions _options;
    private readonly HoldOptions _hold;
    private readonly TimeProvider _time;
    private readonly BunchingTracker _tracker;
    private readonly Dictionary<string, double> _lastAlong = new();
    private DateTimeOffset _lastEvaluation = DateTimeOffset.MinValue;

    private volatile IReadOnlyList<BunchingAlert> _active = Array.Empty<BunchingAlert>();
    private volatile IReadOnlyList<BunchingAlert> _recent = Array.Empty<BunchingAlert>();

    public BunchingDetector(LiveBusState state, IRouteGeometryProvider geometry, IHubContext<BusHub> hub,
        BunchingOptions options, HoldOptions hold, TimeProvider? time = null)
    {
        _hold = hold;
        _state = state;
        _geometry = geometry;
        _hub = hub;
        _options = options;
        _time = time ?? TimeProvider.System;
        _tracker = new BunchingTracker(options);
    }

    public IReadOnlyList<BunchingAlert> Active => _active;
    public IReadOnlyList<BunchingAlert> Recent => _recent;

    public async Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        if (now - _lastEvaluation < TimeSpan.FromSeconds(_options.EvaluationIntervalSeconds)) return;
        _lastEvaluation = now;

        var oldest = now.ToUnixTimeSeconds() - _options.MaxPositionAgeSeconds;
        var fresh = _state.Snapshot().Where(p => p.DirectionId != null && p.Timestamp >= oldest).ToList();

        var pairs = new List<BunchingPair>();
        foreach (var route in fresh.GroupBy(p => p.RouteId))
        {
            if (route.Count() < 2) continue;
            var geometry = await _geometry.GetAsync(route.Key, ct);
            if (geometry == null) continue;

            var onLoop = new List<BusOnLoop>();
            foreach (var bus in route)
            {
                var (along, offset) = geometry.Project(
                    bus.Latitude, bus.Longitude, _lastAlong.TryGetValue(bus.Id, out var hint) ? hint : null);
                if (offset > _options.MaxOffsetMeters) continue;
                _lastAlong[bus.Id] = along;
                onLoop.Add(new BusOnLoop(bus.Id, BunchingAnalyzer.LoopPosition(along, bus.DirectionId!.Value, geometry.LengthMeters), bus.Speed));
            }
            pairs.AddRange(BunchingAnalyzer.ConsecutiveGaps(
                route.Key, route.First().RouteNumber, onLoop, 2 * geometry.LengthMeters, _hold));
        }

        var liveIds = fresh.Select(p => p.Id).ToHashSet();
        foreach (var id in _lastAlong.Keys.Where(id => !liveIds.Contains(id)).ToList()) _lastAlong.Remove(id);

        var changes = _tracker.Update(pairs, now);
        _active = _tracker.Active;
        _recent = _tracker.Recent;

        // alertele active se retrimit periodic (golul se schimbă), iar o încheiere e anunțată imediat
        if (changes.Count > 0 || _active.Count > 0)
            await _hub.Clients.All.SendAsync(BunchingEvent, _active, ct);
    }
}
