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
    /// <summary>Strategia de control: <c>none</c>, <c>headway</c> sau <c>equalize</c> (vezi <see cref="HoldPolicy"/>).</summary>
    public string Strategy { get; set; } = HoldPolicy.None;
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
        if (!HoldPolicy.IsKnown(Strategy)) return $"strategy trebuie să fie una dintre: {string.Join(", ", HoldPolicy.Strategies)}.";
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
    IReadOnlyList<double> FinalLoopPositions,
    string Strategy = HoldPolicy.None,
    /// <summary>cât a reținut în total strategia de control, pe autobuz și pe oră simulată (costul pentru pasagerii din autobuz)</summary>
    double HoldSecondsPerBusHour = 0,
    double AverageSpeedKmh = 0,
    /// <summary>așteptarea medie în stație pe pasager (s)</summary>
    double MeanWaitSeconds = 0,
    /// <summary>așteptarea în plus față de autobuze perfect regulate, Var(H)/(2E[H]) (s)</summary>
    double ExcessWaitSeconds = 0,
    /// <summary>coeficientul de variație al headway-ului în timp, în stații</summary>
    double HeadwayCv = 0,
    double MeanHeadwaySeconds = 0,
    /// <summary>așteptare în stație + întârziere în autobuz din holding, pe pasager (s): metrica principală de comparație</summary>
    double TotalDelayPerPassengerSeconds = 0);

public static class TwinRunner
{
    private const int SampleEverySeconds = 10;
    private const int SeriesEverySeconds = 60;

    /// <summary>Rulează scenariul headless și întoarce metricile. Pur și deterministic pentru același seed.</summary>
    public static TwinRunResult Run(
        TwinConfig config, IReadOnlyList<double> initialLoopPositions, TwinScenario scenario,
        string initialisedFrom, BunchingOptions? options = null, ITwinControl? control = null,
        string strategy = HoldPolicy.None)
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
            engine.Buses.Select(b => Math.Round(engine.LoopPosition(b))).ToList(),
            strategy,
            Math.Round(engine.TotalControlHoldSeconds / engine.Buses.Count / (scenario.DurationSeconds / 3600.0), 1),
            Math.Round(engine.DistanceTravelledMeters / engine.Buses.Count / scenario.DurationSeconds * 3.6, 1),
            Math.Round(engine.MeanWaitSeconds, 1), Math.Round(engine.ExcessWaitSeconds, 1), Math.Round(engine.HeadwayCv, 3),
            Math.Round(engine.MeanHeadwaySeconds, 1), Math.Round(engine.TotalDelayPerPassengerSeconds, 1));
    }

    private static double Cv(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        if (mean <= 0) return 0;
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Sqrt(variance) / mean;
    }
}

/// <summary>Rezultatul unei strategii, mediat pe mai multe rulări cu seed-uri diferite.</summary>
public record TwinStrategySummary(
    string Strategy, double BunchedPercent, double MeanCv, double MinRatio, double BunchingEpisodes,
    double HoldSecondsPerBusHour, double AverageSpeedKmh, IReadOnlyList<TwinSample> Series,
    double MeanWaitSeconds = 0, double ExcessWaitSeconds = 0, double HeadwayCv = 0, double TotalDelayPerPassengerSeconds = 0,
    /// <summary>abaterea standard a întârzierii totale între replicări (cât de sigur e rezultatul)</summary>
    double TotalDelayStdSeconds = 0);

public record TwinComparison(
    int RouteId, string InitialisedFrom, int BusCount, int DurationSeconds, int Replications,
    IReadOnlyList<TwinStrategySummary> Results);

/// <summary>Cerere de comparare: același scenariu rulat cu fiecare strategie, pe mai multe seed-uri.</summary>
public class TwinCompareRequest : TwinScenario
{
    public List<string>? Strategies { get; set; }
    public int Replications { get; set; } = 5;
    public const int MaxReplications = 10;

    public new string? Validate()
    {
        var error = base.Validate();
        if (error != null) return error;
        if (Replications is < 1 or > MaxReplications) return $"replications trebuie să fie între 1 și {MaxReplications}.";
        if (Strategies != null && (Strategies.Count == 0 || Strategies.Any(x => !HoldPolicy.IsKnown(x))))
            return $"strategies trebuie să conțină valori din: {string.Join(", ", HoldPolicy.Strategies)}.";
        return null;
    }
}

/// <summary>Leagă twin-ul de realitate: geometria traseului din baza de date și pozițiile live ale flotei.</summary>
public class TwinService
{
    private readonly IRouteGeometryProvider _geometry;
    private readonly LiveBusState _state;
    private readonly BunchingOptions _options;
    private readonly HoldOptions _hold;
    private readonly TimeProvider _time;

    public TwinService(IRouteGeometryProvider geometry, LiveBusState state, BunchingOptions options, HoldOptions hold,
        TimeProvider? time = null)
    {
        _geometry = geometry;
        _state = state;
        _options = options;
        _hold = hold;
        _time = time ?? TimeProvider.System;
    }

    private sealed record Prepared(TwinConfig Config, IReadOnlyList<double> Initial, string From);

    /// <summary>Rulează scenariul (cu strategia din <see cref="TwinScenario.Strategy"/>); null dacă linia nu are geometrie.</summary>
    public async Task<TwinRunResult?> RunAsync(TwinScenario scenario, CancellationToken ct, ITwinControl? control = null)
    {
        var prepared = await PrepareAsync(scenario, ct);
        return prepared == null ? null : Execute(prepared, scenario, control ?? HoldPolicy.Create(scenario.Strategy, _hold));
    }

    /// <summary>
    /// Compară strategiile pe același scenariu. Fiecare strategie rulează pe <see cref="TwinCompareRequest.Replications"/>
    /// seed-uri consecutive (aceleași pentru toate strategiile) și metricile se mediază, ca să nu depindă de un singur noroc.
    /// </summary>
    public async Task<TwinComparison?> CompareAsync(TwinCompareRequest request, CancellationToken ct)
    {
        var prepared = await PrepareAsync(request, ct);
        if (prepared == null) return null;

        var strategies = request.Strategies ?? HoldPolicy.Strategies.ToList();
        var results = new List<TwinStrategySummary>();
        foreach (var strategy in strategies)
        {
            var runs = Enumerable.Range(0, request.Replications)
                .Select(i => Execute(prepared, Clone(request, request.Seed + i, strategy), HoldPolicy.Create(strategy, _hold)))
                .ToList();
            results.Add(Summarize(strategy, runs));
        }
        return new TwinComparison(request.RouteId, prepared.From, prepared.Initial.Count, request.DurationSeconds,
            request.Replications, results);
    }

    private static TwinScenario Clone(TwinScenario s, int seed, string strategy) => new()
    {
        RouteId = s.RouteId, BusCount = s.BusCount, DurationSeconds = s.DurationSeconds, Seed = seed,
        UseLiveState = s.UseLiveState, DemandFactor = s.DemandFactor, Disturbances = s.Disturbances, Strategy = strategy
    };

    private static TwinStrategySummary Summarize(string strategy, IReadOnlyList<TwinRunResult> runs)
    {
        double Avg(Func<TwinRunResult, double> f) => Math.Round(runs.Average(f), 3);
        var series = runs[0].Series
            .Select((_, i) =>
            {
                var min = runs.Average(r => r.Series[i].MinRatio);
                return new TwinSample(runs[0].Series[i].T, Math.Round(min, 3), Math.Round(runs.Average(r => r.Series[i].Cv), 3),
                    min < new BunchingOptions().EnterRatio);
            })
            .ToList();
        return new TwinStrategySummary(strategy, Math.Round(runs.Average(r => r.BunchedPercent), 1), Avg(r => r.MeanCv),
            Avg(r => r.MinRatio), Math.Round(runs.Average(r => r.BunchingEpisodes), 1),
            Math.Round(runs.Average(r => r.HoldSecondsPerBusHour), 1), Math.Round(runs.Average(r => r.AverageSpeedKmh), 1), series,
            Math.Round(runs.Average(r => r.MeanWaitSeconds), 1), Math.Round(runs.Average(r => r.ExcessWaitSeconds), 1),
            Avg(r => r.HeadwayCv), Math.Round(runs.Average(r => r.TotalDelayPerPassengerSeconds), 1),
            Math.Round(Std(runs.Select(r => r.TotalDelayPerPassengerSeconds).ToList()), 1));
    }

    private static double Std(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }

    private async Task<Prepared?> PrepareAsync(TwinScenario scenario, CancellationToken ct)
    {
        var geometry = await _geometry.GetAsync(scenario.RouteId, ct);
        if (geometry == null) return null;

        var config = new TwinConfig { RouteLengthMeters = geometry.LengthMeters };
        config.PassengerArrivalRate *= scenario.DemandFactor;
        var loop = config.LoopLengthMeters;

        var initial = scenario.UseLiveState ? LivePositions(scenario.RouteId, geometry) : null;
        if (initial is { Count: >= TwinScenario.MinBuses }) return new Prepared(config, initial, "live");

        var n = scenario.BusCount ?? 3;
        return new Prepared(config, Enumerable.Range(0, n).Select(i => i * loop / n).ToList(), "even");
    }

    private TwinRunResult Execute(Prepared p, TwinScenario scenario, ITwinControl control) =>
        TwinRunner.Run(p.Config, p.Initial, scenario, p.From, _options, control, scenario.Strategy);

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
