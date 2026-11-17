namespace TursibBackend.Services;

/// <summary>Un scenariu de rulat în digital twin.</summary>
public class TwinScenario
{
    public int RouteId { get; set; }
    /// <summary>Număr de autobuze (implicit 3). Cu <see cref="UseLiveState"/> și date live, contează flota reală.</summary>
    public int? BusCount { get; set; }
    public int DurationSeconds { get; set; } = 7200;
    public int Seed { get; set; } = 1;
    /// <summary>Pornește de la pozițiile live ale liniei (twin-ul oglindește realitatea), dacă există.</summary>
    public bool UseLiveState { get; set; }
    /// <summary>Multiplicator al cererii de pasageri (1 = normal).</summary>
    public double DemandFactor { get; set; } = 1;
    public List<TwinDisturbance> Disturbances { get; set; } = new();

    public const int MaxDurationSeconds = 6 * 3600;
    public const int MinBuses = 2;
    public const int MaxBuses = 30;
    public const int MaxDisturbances = 10;

    /// <summary>Mesajul de eroare dacă scenariul e invalid, altfel null.</summary>
    public string? Validate()
    {
        if (RouteId <= 0) return "routeId este obligatoriu.";
        if (DurationSeconds is < 60 or > MaxDurationSeconds) return $"durationSeconds trebuie să fie între 60 și {MaxDurationSeconds}.";
        if (BusCount is { } n && (n < MinBuses || n > MaxBuses)) return $"busCount trebuie să fie între {MinBuses} și {MaxBuses}.";
        if (DemandFactor is < 0.1 or > 5) return "demandFactor trebuie să fie între 0,1 și 5.";
        if (Disturbances.Count > MaxDisturbances) return $"Cel mult {MaxDisturbances} perturbări.";
        if (Disturbances.Any(d => d.AtSecond < 0 || d.AtSecond > DurationSeconds || d.HoldSeconds is < 1 or > 3600 || d.BusIndex < 0))
            return "Perturbare invalidă (atSecond în durata rulării, holdSeconds 1..3600, busIndex ≥ 0).";
        return null;
    }
}

/// <summary>Un punct al seriei de timp: cel mai mic raport gol/ideal și coeficientul de variație al golurilor.</summary>
public record TwinSample(int T, double MinRatio, double Cv, bool Bunched);

public record TwinRunResult(
    int RouteId,
    /// <summary>„live” (poziții reale) sau „even” (echidistant)</summary>
    string InitialisedFrom,
    int BusCount,
    int DurationSeconds,
    double RouteLengthMeters,
    /// <summary>% din timp cât cel puțin o pereche a fost „lipită” (raport sub pragul de intrare)</summary>
    double BunchedPercent,
    /// <summary>coeficientul de variație mediu al golurilor (0 = perfect echidistant)</summary>
    double MeanCv,
    double MaxCv,
    double MinRatio,
    /// <summary>câte alerte de bunching (cu aceeași persistență ca în producție) ar fi apărut</summary>
    int BunchingEpisodes,
    IReadOnlyList<TwinSample> Series,
    IReadOnlyList<double> FinalLoopPositions);

public static class TwinRunner
{
    private const int SampleEverySeconds = 10;
    private const int SeriesEverySeconds = 60;

    /// <summary>Rulează scenariul headless și întoarce metricile. Pur și deterministic pentru același seed.</summary>
    public static TwinRunResult Run(
        TwinConfig config, IReadOnlyList<double> initialLoopPositions, TwinScenario scenario,
        string initialisedFrom, BunchingOptions? options = null, ITwinControl? control = null)
    {
        options ??= new BunchingOptions();
        var engine = new TwinEngine(config, initialLoopPositions, scenario.Seed, scenario.Disturbances, control);
        var tracker = new BunchingTracker(options);
        var epoch = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var loop = config.LoopLengthMeters;

        var series = new List<TwinSample>();
        double bunchedSamples = 0, samples = 0, cvSum = 0, maxCv = 0, minRatio = double.MaxValue;
        var episodes = 0;

        for (var t = 0; t <= scenario.DurationSeconds; t++)
        {
            if (t % SampleEverySeconds == 0)
            {
                var onLoop = engine.Buses.Select(b => new BusOnLoop(b.Id, engine.LoopPosition(b), b.Speed * 3.6)).ToList();
                var pairs = BunchingAnalyzer.ConsecutiveGaps(scenario.RouteId, null, onLoop, loop);
                var sampleMin = pairs.Min(p => p.Ratio);
                var cv = Cv(pairs.Select(p => p.GapMeters).ToList());
                var bunched = sampleMin < options.EnterRatio;

                samples++;
                if (bunched) bunchedSamples++;
                cvSum += cv;
                maxCv = Math.Max(maxCv, cv);
                minRatio = Math.Min(minRatio, sampleMin);
                episodes += tracker.Update(pairs, epoch.AddSeconds(t)).Count(c => c.Started);

                if (t % SeriesEverySeconds == 0)
                    series.Add(new TwinSample(t, Math.Round(sampleMin, 3), Math.Round(cv, 3), bunched));
            }
            engine.Step();
        }

        return new TwinRunResult(
            scenario.RouteId, initialisedFrom, engine.Buses.Count, scenario.DurationSeconds,
            Math.Round(config.RouteLengthMeters), Math.Round(100 * bunchedSamples / samples, 1),
            Math.Round(cvSum / samples, 3), Math.Round(maxCv, 3), Math.Round(minRatio, 3), episodes, series,
            engine.Buses.Select(b => Math.Round(engine.LoopPosition(b))).ToList());
    }

    private static double Cv(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        if (mean <= 0) return 0;
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Sqrt(variance) / mean;
    }
}

/// <summary>Leagă twin-ul de realitate: geometria traseului din baza de date și pozițiile live ale flotei.</summary>
public class TwinService
{
    private readonly IRouteGeometryProvider _geometry;
    private readonly LiveBusState _state;
    private readonly BunchingOptions _options;
    private readonly TimeProvider _time;

    public TwinService(IRouteGeometryProvider geometry, LiveBusState state, BunchingOptions options, TimeProvider? time = null)
    {
        _geometry = geometry;
        _state = state;
        _options = options;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Rulează scenariul; null dacă linia nu are geometrie (nu o putem modela).</summary>
    public async Task<TwinRunResult?> RunAsync(TwinScenario scenario, CancellationToken ct, ITwinControl? control = null)
    {
        var geometry = await _geometry.GetAsync(scenario.RouteId, ct);
        if (geometry == null) return null;

        var config = new TwinConfig { RouteLengthMeters = geometry.LengthMeters };
        config.PassengerArrivalRate *= scenario.DemandFactor;
        var loop = config.LoopLengthMeters;

        var initial = scenario.UseLiveState ? LivePositions(scenario.RouteId, geometry) : null;
        if (initial is { Count: >= TwinScenario.MinBuses })
            return TwinRunner.Run(config, initial, scenario, "live", _options, control);

        var n = scenario.BusCount ?? 3;
        var even = Enumerable.Range(0, n).Select(i => i * loop / n).ToList();
        return TwinRunner.Run(config, even, scenario, "even", _options, control);
    }

    /// <summary>Pozițiile reale ale liniei pe bucla dus-întors (doar autobuze recente, cu direcție, aproape de traseu).</summary>
    private List<double>? LivePositions(int routeId, RouteGeometry geometry)
    {
        var oldest = _time.GetUtcNow().ToUnixTimeSeconds() - _options.MaxPositionAgeSeconds;
        var positions = new List<double>();
        foreach (var bus in _state.Snapshot().Where(p => p.RouteId == routeId && p.DirectionId != null && p.Timestamp >= oldest))
        {
            var (along, offset) = geometry.Project(bus.Latitude, bus.Longitude);
            if (offset > _options.MaxOffsetMeters) continue;
            positions.Add(BunchingAnalyzer.LoopPosition(along, bus.DirectionId!.Value, geometry.LengthMeters));
        }
        return positions.Count == 0 ? null : positions;
    }
}
