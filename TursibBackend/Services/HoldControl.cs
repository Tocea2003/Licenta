namespace TursibBackend.Services;

// Controlul bunching-ului prin „holding”: un autobuz prea aproape de cel din față e reținut la stație ca să
// redea golul. Aceeași politică rulează în digital twin (unde strategiile se compară) și produce
// recomandările în timp real pentru dispecer.

/// <summary>Parametrii politicii de holding (secțiunea de configurare „Holding”).</summary>
public class HoldOptions
{
    /// <summary>Se intervine doar când golul față de autobuzul din față e sub această fracțiune din golul ideal.</summary>
    public double TriggerRatio { get; set; } = 0.85;
    /// <summary>Reținerea maximă la o singură stație (pasagerii din autobuz așteaptă).</summary>
    public double MaxHoldSeconds { get; set; } = 45;
    /// <summary>Viteza minimă (m/s) folosită la calcul, ca să nu împărțim la ~0 pentru un autobuz oprit.</summary>
    public double MinSpeedMs { get; set; } = 4;
}

public static class HoldPolicy
{
    public const string None = "none";
    public const string Headway = "headway";
    public const string Equalize = "equalize";
    /// <summary>Q-learning antrenat în digital twin (vezi <see cref="RlHoldControl"/>).</summary>
    public const string Reinforcement = "rl";

    public static readonly IReadOnlyList<string> Strategies = new[] { None, Headway, Equalize, Reinforcement };

    /// <summary>
    /// „Forward headway”: reține autobuzul cât să recupereze golul lipsă față de cel ideal.
    /// Cât stă reținut, liderul se depărtează cu viteza lui, deci timpul = deficit / viteză.
    /// </summary>
    public static double HeadwayHold(double gapAhead, double ideal, double speedMs, HoldOptions o)
    {
        if (gapAhead >= o.TriggerRatio * ideal) return 0;
        return Math.Clamp((ideal - gapAhead) / Math.Max(speedMs, o.MinSpeedMs), 0, o.MaxHoldSeconds);
    }

    /// <summary>
    /// „Equalize”: reține autobuzul cât să egalizeze golul din față cu cel din spate. Cât stă reținut, golul din față
    /// crește cu v·h, iar cel din spate scade cu v·h, deci h = (gapBehind − gapAhead) / 2v.
    /// Nu reține dacă golul din spate e deja mai mic (ar înrăutăți situația perechii din spate).
    /// </summary>
    public static double EqualizeHold(double gapAhead, double gapBehind, double ideal, double speedMs, HoldOptions o)
    {
        if (gapAhead >= o.TriggerRatio * ideal) return 0;
        return Math.Clamp((gapBehind - gapAhead) / (2 * Math.Max(speedMs, o.MinSpeedMs)), 0, o.MaxHoldSeconds);
    }

    /// <summary>
    /// Recomandarea pentru dispecer: pentru un autobuz cu golul din față <paramref name="gapAhead"/>, cât să stea la următoarea stație.
    /// Folosește strategia „headway”, cea mai bună în comparațiile din digital twin (neuniformitate mai mică la flote mari, cost similar).
    /// </summary>
    public static double Recommend(double gapAhead, double ideal, double speedMs, HoldOptions o) =>
        HeadwayHold(gapAhead, ideal, speedMs, o);

    public static bool IsKnown(string? strategy) => strategy != null && Strategies.Contains(strategy);

    public static ITwinControl Create(string strategy, HoldOptions options) => strategy switch
    {
        Headway => new HeadwayHoldControl(options),
        Equalize => new EqualizeHoldControl(options),
        Reinforcement => RlHoldControl.Greedy(QPolicy.Default),
        _ => new NoControl()
    };
}

public sealed class HeadwayHoldControl : ITwinControl
{
    private readonly HoldOptions _options;
    public HeadwayHoldControl(HoldOptions options) => _options = options;

    public double ExtraHoldSeconds(TwinStopContext c) =>
        HoldPolicy.HeadwayHold(c.GapAheadMeters, c.IdealGapMeters, c.Config.CruiseSpeedMs, _options);
}

public sealed class EqualizeHoldControl : ITwinControl
{
    private readonly HoldOptions _options;
    public EqualizeHoldControl(HoldOptions options) => _options = options;

    public double ExtraHoldSeconds(TwinStopContext c) =>
        HoldPolicy.EqualizeHold(c.GapAheadMeters, c.GapBehindMeters, c.IdealGapMeters, c.Config.CruiseSpeedMs, _options);
}
