using System.Text;
using System.Text.Json;

namespace TursibBackend.Services;

/// <summary>
/// Experimentul principal al proiectului: aceleași scenarii și aceleași seed-uri de test pentru fiecare strategie de control,
/// ca diferențele să vină din strategie, nu din noroc. Rezultatele (medii, abateri, intervale de încredere) sunt scrise în
/// <c>docs/results</c> de comanda <c>dotnet run -- twin-experiment</c>.
/// </summary>
public static class TwinExperiment
{
    public record Scenario(string Name, int Buses, bool Disturbed);

    public record Metric(double Mean, double Std);

    public record StrategyResult(
        string Strategy, Metric TotalDelay, Metric MeanWait, Metric ExcessWait, Metric HeadwayCv,
        Metric HoldSecondsPerBusHour, Metric BunchedPercent);

    /// <summary>Diferența pereche (pe același seed) a întârzierii totale față de holding-ul clasic, cu interval de încredere 95%.</summary>
    public record PairedDifference(string Strategy, double MeanDifference, double CiLow, double CiHigh, int Better, int Worse);

    public record ScenarioResult(Scenario Scenario, IReadOnlyList<StrategyResult> Strategies, IReadOnlyList<PairedDifference> VersusHeadway);

    /// <summary>Un scenariu what-if față de linia de bază, cu holding clasic activ (cum ar fi în exploatare).</summary>
    public record WhatIfResult(string Name, int BusesAtStart, int BusesAtEnd, Metric MeanHeadway, Metric MeanWait,
        Metric TotalDelay, Metric HeadwayCv, Metric SkippedStops);

    public sealed record Report(
        int Replications, int DurationSeconds, double RouteLengthMeters, string TestSeeds, string Note,
        IReadOnlyList<ScenarioResult> Scenarios, IReadOnlyList<WhatIfResult>? WhatIf = null)
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        public string ToJson() => JsonSerializer.Serialize(this, Json);

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Rezultate: controlul bunching-ului în digital twin");
            sb.AppendLine();
            sb.AppendLine($"Generat cu `dotnet run -- twin-experiment` (din `TursibBackend/`). {Note}");
            sb.AppendLine($"Linie de {RouteLengthMeters / 1000:F1} km pe sens, {DurationSeconds / 3600} h simulate, {Replications} replicări pe scenariu (seed-uri de test {TestSeeds}).");
            sb.AppendLine("Valorile sunt medie ± abatere standard între replicări. Întârziere = așteptare în stație + timp pierdut în autobuz din holding, pe pasager.");
            sb.AppendLine();
            foreach (var s in Scenarios)
            {
                sb.AppendLine($"## {s.Scenario.Name}");
                sb.AppendLine();
                sb.AppendLine("| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                foreach (var r in s.Strategies)
                    sb.AppendLine($"| {r.Strategy} | {F(r.TotalDelay, 0)} | {F(r.MeanWait, 0)} | {F(r.ExcessWait, 0)} | {F(r.HeadwayCv, 2)} | {F(r.HoldSecondsPerBusHour, 0)} | {F(r.BunchedPercent, 1)} |");
                sb.AppendLine();
                foreach (var d in s.VersusHeadway)
                    sb.AppendLine($"- {d.Strategy} − headway: {d.MeanDifference:+0.0;-0.0} s/pasager (IÎ 95%: {d.CiLow:+0.0;-0.0} … {d.CiHigh:+0.0;-0.0}); mai bun pe {d.Better}, mai slab pe {d.Worse} din {Replications} seed-uri");
                sb.AppendLine();
            }
            if (WhatIf is { Count: > 0 })
            {
                sb.AppendLine("## Scenarii what-if (holding clasic activ)");
                sb.AppendLine();
                sb.AppendLine("| scenariu | autobuze | headway mediu (s) | așteptare medie (s) | întârziere / pasager (s) | CV headway | opriri sărite |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                foreach (var w in WhatIf)
                    sb.AppendLine($"| {w.Name} | {w.BusesAtStart}→{w.BusesAtEnd} | {F(w.MeanHeadway, 0)} | {F(w.MeanWait, 0)} | {F(w.TotalDelay, 0)} | {F(w.HeadwayCv, 2)} | {F(w.SkippedStops, 0)} |");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string F(Metric m, int decimals) =>
            $"{m.Mean.ToString($"F{decimals}")} ± {m.Std.ToString($"F{decimals}")}";
    }

    public static readonly IReadOnlyList<Scenario> Scenarios = new[]
    {
        new Scenario("3 autobuze, doar zgomot", 3, false),
        new Scenario("3 autobuze, un autobuz oprit 5 min", 3, true),
        new Scenario("5 autobuze, doar zgomot", 5, false),
        new Scenario("5 autobuze, un autobuz oprit 5 min", 5, true),
        new Scenario("8 autobuze, doar zgomot", 8, false),
        new Scenario("8 autobuze, un autobuz oprit 5 min", 8, true)
    };

    public static Report Run(int replications = 20, Action<string>? log = null, int durationSeconds = 4 * 3600)
    {
        var config = new TwinConfig { RouteLengthMeters = 5000 };
        var holdOptions = new HoldOptions();
        var results = new List<ScenarioResult>();

        foreach (var scenario in Scenarios)
        {
            log?.Invoke($"   {scenario.Name}...");
            var perStrategy = new Dictionary<string, List<TwinRunResult>>();
            foreach (var strategy in HoldPolicy.Strategies)
            {
                var runs = new List<TwinRunResult>();
                for (var r = 0; r < replications; r++)
                {
                    var twin = new TwinScenario
                    {
                        RouteId = 0, DurationSeconds = durationSeconds, Seed = RlTrainer.EvaluationSeedBase + r, Strategy = strategy
                    };
                    if (scenario.Disturbed) twin.Disturbances.Add(new TwinDisturbance(600, 0, 300));
                    var initial = Enumerable.Range(0, scenario.Buses).Select(i => i * config.LoopLengthMeters / scenario.Buses).ToList();
                    runs.Add(TwinRunner.Run(config, initial, twin, "even", control: HoldPolicy.Create(strategy, holdOptions), strategy: strategy));
                }
                perStrategy[strategy] = runs;
            }

            var strategies = perStrategy.Select(kv => new StrategyResult(kv.Key,
                M(kv.Value, r => r.TotalDelayPerPassengerSeconds), M(kv.Value, r => r.MeanWaitSeconds), M(kv.Value, r => r.ExcessWaitSeconds),
                M(kv.Value, r => r.HeadwayCv), M(kv.Value, r => r.HoldSecondsPerBusHour), M(kv.Value, r => r.BunchedPercent))).ToList();

            var headway = perStrategy[HoldPolicy.Headway];
            var versus = perStrategy.Where(kv => kv.Key != HoldPolicy.Headway)
                .Select(kv => Paired(kv.Key, kv.Value, headway)).ToList();
            results.Add(new ScenarioResult(scenario, strategies, versus));
        }

        log?.Invoke("   scenarii what-if...");
        return new Report(replications, durationSeconds, config.RouteLengthMeters,
            $"{RlTrainer.EvaluationSeedBase}–{RlTrainer.EvaluationSeedBase + replications - 1}",
            "Politica RL a fost antrenată pe alte seed-uri și scenarii (vezi RlTrainer) și aleasă pe un set de validare separat.",
            results, RunWhatIf(replications, durationSeconds));
    }

    /// <summary>What-if pe aceeași linie (5 km/sens): bază 5 autobuze, cu holding clasic, cu zgomot.</summary>
    public static IReadOnlyList<WhatIfResult> RunWhatIf(int replications, int durationSeconds)
    {
        var config = new TwinConfig { RouteLengthMeters = 5000 };
        var cases = new (string Name, Func<TwinScenario, TwinScenario> Apply, int? Buses)[]
        {
            ("bază: 5 autobuze", s => s, 5),
            ("+1 autobuz de la început", s => { s.ExtraBuses.Add(new TwinBusInjection(0, 1)); return s; }, 5),
            ("+2 autobuze după 1 h", s => { s.ExtraBuses.Add(new TwinBusInjection(3600, 2)); return s; }, 5),
            ("frecvență: un autobuz la 6 min", s => s, null),
            ("frecvență: un autobuz la 10 min", s => s, null),
            ("stradă închisă km 1,5–2,5, ocol +1,2 km", s => { s.Closures.Add(new TwinClosure(1500, 2500, 1200)); return s; }, 5),
            ("stradă închisă 1 h (de la 1 h), ocol +1,2 km", s => { s.Closures.Add(new TwinClosure(1500, 2500, 1200, 3600, 7200)); return s; }, 5)
        };
        var headwayFor = new Dictionary<string, double> { ["frecvență: un autobuz la 6 min"] = 6, ["frecvență: un autobuz la 10 min"] = 10 };

        var results = new List<WhatIfResult>();
        foreach (var (name, apply, buses) in cases)
        {
            var n = buses ?? TwinPlanning.BusesForHeadway(config, headwayFor[name] * 60);
            var runs = Enumerable.Range(0, replications).Select(r =>
            {
                var scenario = apply(new TwinScenario
                {
                    RouteId = 0, DurationSeconds = durationSeconds, Seed = RlTrainer.EvaluationSeedBase + r, Strategy = HoldPolicy.Headway
                });
                var initial = Enumerable.Range(0, n).Select(i => i * config.LoopLengthMeters / n).ToList();
                return TwinRunner.Run(config, initial, scenario, "even", control: HoldPolicy.Create(HoldPolicy.Headway, new HoldOptions()),
                    strategy: HoldPolicy.Headway);
            }).ToList();
            results.Add(new WhatIfResult(name, n, runs[0].FinalBusCount, M(runs, r => r.MeanHeadwaySeconds), M(runs, r => r.MeanWaitSeconds),
                M(runs, r => r.TotalDelayPerPassengerSeconds), M(runs, r => r.HeadwayCv), M(runs, r => r.SkippedStopVisits)));
        }
        return results;
    }

    private static Metric M(IReadOnlyList<TwinRunResult> runs, Func<TwinRunResult, double> f)
    {
        var values = runs.Select(f).ToList();
        return new Metric(Math.Round(values.Average(), 3), Math.Round(Std(values), 3));
    }

    public static PairedDifference Paired(string strategy, IReadOnlyList<TwinRunResult> runs, IReadOnlyList<TwinRunResult> baseline)
    {
        var diffs = runs.Zip(baseline, (a, b) => a.TotalDelayPerPassengerSeconds - b.TotalDelayPerPassengerSeconds).ToList();
        var mean = diffs.Average();
        var half = TCritical95(diffs.Count - 1) * Std(diffs) / Math.Sqrt(diffs.Count);
        return new PairedDifference(strategy, Math.Round(mean, 2), Math.Round(mean - half, 2), Math.Round(mean + half, 2),
            diffs.Count(d => d < 0), diffs.Count(d => d > 0));
    }

    private static double Std(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }

    /// <summary>Cuantila 97,5% a distribuției t (pentru intervalul de încredere 95% al diferenței medii).</summary>
    public static double TCritical95(int degreesOfFreedom) => degreesOfFreedom switch
    {
        <= 0 => double.NaN,
        1 => 12.706, 2 => 4.303, 3 => 3.182, 4 => 2.776, 5 => 2.571, 6 => 2.447, 7 => 2.365, 8 => 2.306, 9 => 2.262,
        10 => 2.228, 11 => 2.201, 12 => 2.179, 13 => 2.160, 14 => 2.145, 15 => 2.131, 16 => 2.120, 17 => 2.110, 18 => 2.101,
        19 => 2.093, 20 => 2.086, <= 25 => 2.06, <= 30 => 2.042, <= 60 => 2.0, _ => 1.96
    };
}
