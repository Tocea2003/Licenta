using System.Reflection;
using System.Text.Json;

namespace TursibBackend.Services;

// Controlul bunching-ului prin reinforcement learning: Q-learning tabular, antrenat în digital twin.
//
// Decizia se ia la fiecare sosire a unui autobuz într-o stație (ca la holding-ul clasic), cu o politică comună pentru
// toate autobuzele (agenți identici, independenți, cu aceeași tabelă Q):
//   stare     = (golul față de autobuzul din față, golul față de cel din spate), raportate la golul ideal, discretizate;
//   acțiune   = cât îl reținem în stație: 0, 5, 15, 30 sau 45 s;
//   recompensă = -(așteptarea în exces produsă la stația următoare + întârzierea pasagerilor din autobuz din cauza
//                reținerii), în pasager·secunde: exact costul după care comparăm strategiile.
// Actualizarea e de tip SMDP: recompensa și starea următoare se observă la următoarea decizie a aceluiași autobuz.

/// <summary>Tabela Q plus felul în care se discretizează starea și ce acțiuni există.</summary>
public sealed class QPolicy
{
    /// <summary>Limitele intervalelor pentru raportul gol / gol ideal (ultimul interval e deschis).</summary>
    public double[] RatioBins { get; init; } = { 0.25, 0.5, 0.75, 0.9, 1.1, 1.3, 1.6 };
    public double[] HoldActions { get; init; } = { 0, 5, 15, 30, 45 };
    /// <summary>Valorile Q, [stare][acțiune].</summary>
    public double[][] Q { get; init; } = Array.Empty<double[]>();
    /// <summary>De câte ori a fost actualizată fiecare pereche (stare, acțiune) la antrenare.</summary>
    public int[][] Visits { get; init; } = Array.Empty<int[]>();
    public PolicyInfo Info { get; init; } = new();

    public sealed class PolicyInfo
    {
        public int Episodes { get; init; }
        public int Seed { get; init; }
        public double ExcessWeight { get; init; }
        public string TrainedOn { get; init; } = string.Empty;
    }

    public int BinCount => RatioBins.Length + 1;
    public int StateCount => BinCount * BinCount;

    public static QPolicy Empty(int episodes = 0, int seed = 0, double excessWeight = 1)
    {
        var p = new QPolicy { Info = new PolicyInfo { Episodes = episodes, Seed = seed, ExcessWeight = excessWeight } };
        return new QPolicy
        {
            Info = p.Info,
            Q = Enumerable.Range(0, p.StateCount).Select(_ => new double[p.HoldActions.Length]).ToArray(),
            Visits = Enumerable.Range(0, p.StateCount).Select(_ => new int[p.HoldActions.Length]).ToArray()
        };
    }

    public int Bin(double ratio)
    {
        for (var i = 0; i < RatioBins.Length; i++) if (ratio < RatioBins[i]) return i;
        return RatioBins.Length;
    }

    public int State(double gapAheadRatio, double gapBehindRatio) => Bin(gapAheadRatio) * BinCount + Bin(gapBehindRatio);

    /// <summary>Acțiunea cu cea mai mare valoare Q; la egalitate, reținerea cea mai mică (nu ține autobuzul degeaba).</summary>
    public int Greedy(int state)
    {
        var row = Q[state];
        var best = 0;
        for (var a = 1; a < row.Length; a++) if (row[a] > row[best] + 1e-9) best = a;
        return best;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static QPolicy FromJson(string json) => JsonSerializer.Deserialize<QPolicy>(json, Json)
        ?? throw new InvalidDataException("Politică RL invalidă");

    private static QPolicy? _default;

    /// <summary>Politica antrenată livrată cu aplicația (Resources/rl-hold-policy.json, resursă încorporată).</summary>
    public static QPolicy Default
    {
        get
        {
            if (_default != null) return _default;
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("rl-hold-policy.json", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Lipsește politica RL antrenată (Resources/rl-hold-policy.json).");
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            return _default = FromJson(reader.ReadToEnd());
        }
    }
}

public sealed class RlOptions
{
    /// <summary>Rata de învățare α la începutul antrenării; scade liniar până la <see cref="LearningRateEnd"/>.</summary>
    public double LearningRate { get; init; } = 0.2;
    public double LearningRateEnd { get; init; } = 0.02;
    /// <summary>Factorul de discount γ pentru decizia următoare.</summary>
    public double Discount { get; init; } = 0.9;
    /// <summary>Cât cântărește așteptarea în exces față de întârzierea din autobuz în recompensă.</summary>
    public double ExcessWeight { get; init; } = 3.0;
}

/// <summary>
/// Strategia RL ca <see cref="ITwinControl"/>. În modul de antrenare explorează (ε-greedy) și actualizează tabela;
/// în modul de evaluare alege mereu acțiunea cea mai bună și nu modifică tabela.
/// </summary>
public sealed class RlHoldControl : ITwinControl
{
    private readonly QPolicy _policy;
    private readonly RlOptions _options;
    private readonly Random? _random;
    private readonly bool _learn;
    private readonly Dictionary<int, (int State, int Action, double OnBoardCost)> _pending = new();

    public double Epsilon { get; set; }
    public double Alpha { get; set; }

    private RlHoldControl(QPolicy policy, RlOptions options, bool learn, Random? random)
    {
        _policy = policy;
        _options = options;
        _learn = learn;
        _random = random;
    }

    /// <summary>Politica fixă (evaluare și recomandări): fără explorare, fără învățare.</summary>
    public static RlHoldControl Greedy(QPolicy policy) => new(policy, new RlOptions(), learn: false, random: null);

    public static RlHoldControl Learner(QPolicy policy, RlOptions options, Random random, double epsilon, double? alpha = null) =>
        new(policy, options, learn: true, random) { Epsilon = epsilon, Alpha = alpha ?? options.LearningRate };

    public double ExtraHoldSeconds(TwinStopContext c)
    {
        var ideal = c.IdealGapMeters > 0 ? c.IdealGapMeters : 1;
        var state = _policy.State(c.GapAheadMeters / ideal, c.GapBehindMeters / ideal);

        if (_learn)
        {
            // recompensa deciziei precedente a acestui autobuz, observată acum, la stația următoare
            if (_pending.TryGetValue(c.BusIndex, out var previous))
            {
                var reward = -(_options.ExcessWeight * ExcessWaitCost(c) + previous.OnBoardCost);
                var target = reward + _options.Discount * _policy.Q[state].Max();
                var q = _policy.Q[previous.State];
                q[previous.Action] += Alpha * (target - q[previous.Action]);
                _policy.Visits[previous.State][previous.Action]++;
            }
        }

        var action = _learn && _random!.NextDouble() < Epsilon
            ? _random.Next(_policy.HoldActions.Length)
            : _policy.Greedy(state);
        var hold = _policy.HoldActions[action];

        if (_learn) _pending[c.BusIndex] = (state, action, c.Bus.Load * hold);
        return hold;
    }

    /// <summary>
    /// Așteptarea în exces (pasager·secunde) la stația la care tocmai a sosit autobuzul: λ(H − Ĥ)²/2, unde Ĥ e headway-ul
    /// mediu. Pentru un headway H, pasagerii au așteptat λH²/2; față de un headway regulat Ĥ diferența medie e λ(H−Ĥ)²/2.
    /// </summary>
    public static double ExcessWaitCost(TwinStopContext c)
    {
        if (c.MeanHeadwaySeconds <= 0) return 0;
        var d = c.HeadwaySeconds - c.MeanHeadwaySeconds;
        return c.Config.PassengerArrivalRate * d * d / 2;
    }
}

/// <summary>Antrenarea politicii RL în digital twin, pe scenarii variate și reproductibile (seed).</summary>
public static class RlTrainer
{
    /// <summary>
    /// Seturi de seed-uri disjuncte: antrenare (100 000+), validare pentru alegerea politicii (50 000+) și test pentru
    /// rezultatele raportate (1+). Politica nu vede niciodată seed-urile de test.
    /// </summary>
    public const int TrainingSeedBase = 100_000;
    public const int ValidationSeedBase = 50_000;
    public const int EvaluationSeedBase = 1;

    /// <summary>
    /// Q-learning tabular depinde de seed (ordinea explorării). Antrenăm mai mulți candidați și îl păstrăm pe cel cu cea mai
    /// mică întârziere medie pe pasager pe scenariile de validare.
    /// </summary>
    public static QPolicy TrainSelected(int candidates, int episodes, int baseSeed = 42, RlOptions? options = null,
        Action<string>? log = null)
    {
        QPolicy? best = null;
        var bestScore = double.MaxValue;
        for (var c = 0; c < candidates; c++)
        {
            var seed = baseSeed + 1000 * c;
            var policy = Train(episodes, seed, options);
            var score = Validate(policy);
            log?.Invoke($"   candidatul {c + 1}/{candidates} (seed {seed}): {score:F1} s întârziere/pasager pe validare");
            if (score < bestScore) { bestScore = score; best = policy; }
        }
        return best!;
    }

    /// <summary>Întârzierea medie pe pasager a politicii pe scenariile de validare (alte seed-uri decât la test).</summary>
    public static double Validate(QPolicy policy, int replications = 4)
    {
        var scores = new List<double>();
        foreach (var (buses, disturbed) in new[] { (3, false), (4, true), (6, true), (8, false) })
            for (var r = 0; r < replications; r++)
            {
                var config = new TwinConfig { RouteLengthMeters = 5000 };
                var initial = Enumerable.Range(0, buses).Select(i => i * config.LoopLengthMeters / buses).ToList();
                var disturbances = disturbed ? new[] { new TwinDisturbance(600, 0, 300) } : null;
                var engine = new TwinEngine(config, initial, ValidationSeedBase + r, disturbances, RlHoldControl.Greedy(policy));
                for (var t = 0; t < 4 * 3600; t++) engine.Step();
                scores.Add(engine.TotalDelayPerPassengerSeconds);
            }
        return scores.Average();
    }

    public static QPolicy Train(int episodes, int seed = 42, RlOptions? options = null, Action<int, double>? progress = null)
    {
        options ??= new RlOptions();
        var policy = QPolicy.Empty(episodes, seed, options.ExcessWeight);
        var scenarioRandom = new Random(seed);
        var explorationRandom = new Random(seed + 1);

        for (var e = 0; e < episodes; e++)
        {
            // explorare care scade liniar de la 0,3 la 0,02
            var progressFraction = (double)e / Math.Max(1, episodes - 1);
            var epsilon = 0.3 - 0.28 * progressFraction;
            var alpha = options.LearningRate + (options.LearningRateEnd - options.LearningRate) * progressFraction;
            var n = scenarioRandom.Next(3, 9);
            var config = new TwinConfig
            {
                RouteLengthMeters = 3000 + scenarioRandom.NextDouble() * 5000,
                PassengerArrivalRate = 0.006 + scenarioRandom.NextDouble() * 0.004
            };
            var loop = config.LoopLengthMeters;
            var initial = Enumerable.Range(0, n).Select(i => i * loop / n).ToList();
            var disturbances = scenarioRandom.NextDouble() < 0.6
                ? new[] { new TwinDisturbance(scenarioRandom.Next(300, 3600), scenarioRandom.Next(n), scenarioRandom.Next(120, 420)) }
                : Array.Empty<TwinDisturbance>();

            var learner = RlHoldControl.Learner(policy, options, explorationRandom, epsilon, alpha);
            var engine = new TwinEngine(config, initial, TrainingSeedBase + e, disturbances, learner);
            for (var t = 0; t < 4 * 3600; t++) engine.Step();
            progress?.Invoke(e, engine.TotalDelayPerPassengerSeconds);
        }
        return policy;
    }
}
