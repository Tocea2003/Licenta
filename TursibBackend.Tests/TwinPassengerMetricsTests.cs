using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class TwinPassengerMetricsTests
{
    private const double Loop = 10_000;
    private static List<double> Even(int n) => Enumerable.Range(0, n).Select(i => i * Loop / n).ToList();

    private static TwinEngine Run(double dwellJitter, int seconds, TwinDisturbance? disturbance = null, ITwinControl? control = null, int seed = 1)
    {
        var engine = new TwinEngine(new TwinConfig { RouteLengthMeters = Loop / 2, DwellJitter = dwellJitter }, Even(3), seed,
            disturbance == null ? null : new[] { disturbance }, control);
        for (var t = 0; t < seconds; t++) engine.Step();
        return engine;
    }

    [Fact]
    public void MeanWait_IsHalfTheMeanHeadwayPlusTheExcessWait()
    {
        // identitate: E[H²]/(2E[H]) = E[H]/2 + Var(H)/(2E[H])
        var e = Run(dwellJitter: 0.3, seconds: 14_400, disturbance: new TwinDisturbance(600, 0, 300));
        Assert.Equal(e.MeanHeadwaySeconds / 2 + e.ExcessWaitSeconds, e.MeanWaitSeconds, 6);
    }

    [Fact]
    public void ARegularFleet_HasAlmostNoExcessWait()
    {
        var e = Run(dwellJitter: 0, seconds: 14_400);
        Assert.True(e.HeadwayCv < 0.1, $"CV {e.HeadwayCv}");
        Assert.True(e.ExcessWaitSeconds < 0.05 * e.MeanWaitSeconds, $"excess {e.ExcessWaitSeconds} of {e.MeanWaitSeconds}");
        Assert.True(e.PassengersServed > 0);
    }

    [Fact]
    public void Bunching_IsPaidInWaitingTime()
    {
        var regular = Run(dwellJitter: 0, seconds: 14_400);
        var bunched = Run(dwellJitter: 0, seconds: 14_400, disturbance: new TwinDisturbance(600, 0, 300));

        Assert.True(bunched.HeadwayCv > 3 * regular.HeadwayCv);
        Assert.True(bunched.MeanWaitSeconds > 1.3 * regular.MeanWaitSeconds,
            $"regular {regular.MeanWaitSeconds:F0}s, bunched {bunched.MeanWaitSeconds:F0}s");
    }

    [Fact]
    public void Holding_CostsOnBoardDelay_OnlyWhenItHolds()
    {
        var none = Run(0.3, 7200, new TwinDisturbance(600, 0, 300));
        var held = Run(0.3, 7200, new TwinDisturbance(600, 0, 300), new HeadwayHoldControl(new HoldOptions()));

        Assert.Equal(0, none.OnBoardHoldPassengerSeconds);
        Assert.True(held.OnBoardHoldPassengerSeconds > 0);
        Assert.True(held.TotalDelayPerPassengerSeconds > held.MeanWaitSeconds);
        Assert.Equal(none.MeanWaitSeconds, none.TotalDelayPerPassengerSeconds, 6);
    }

    [Fact]
    public void RunnerReportsThePassengerMetrics()
    {
        var scenario = new TwinScenario { RouteId = 1, DurationSeconds = 3600 };
        var r = TwinRunner.Run(new TwinConfig { RouteLengthMeters = Loop / 2 }, Even(3), scenario, "even");
        Assert.True(r.MeanWaitSeconds > 0);
        Assert.True(r.MeanHeadwaySeconds > 0);
        Assert.InRange(r.TotalDelayPerPassengerSeconds, r.MeanWaitSeconds - 0.11, r.MeanWaitSeconds + 0.11);
    }
}
