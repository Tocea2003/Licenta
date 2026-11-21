using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class RlHoldControlTests
{
    [Theory]
    [InlineData(0.1, 0)]
    [InlineData(0.25, 1)]
    [InlineData(0.8, 3)]
    [InlineData(1.0, 4)]
    [InlineData(5.0, 7)]
    public void RatiosAreDiscretizedIntoEightBins(double ratio, int bin) =>
        Assert.Equal(bin, QPolicy.Empty().Bin(ratio));

    [Fact]
    public void StateCombinesTheFrontAndBackGaps()
    {
        var p = QPolicy.Empty();
        Assert.Equal(64, p.StateCount);
        Assert.Equal(0, p.State(0.1, 0.1));
        Assert.Equal(4 * 8 + 7, p.State(1.0, 3.0));
    }

    [Fact]
    public void Greedy_PicksTheBestAction_AndTheSmallestHoldOnTies()
    {
        var p = QPolicy.Empty();
        Assert.Equal(0, p.Greedy(5));                       // toate egale: nu ține autobuzul
        p.Q[5][3] = 1;
        Assert.Equal(3, p.Greedy(5));
    }

    [Fact]
    public void Learner_UpdatesTheValueOfThePreviousDecision_WhenTheSameBusDecidesAgain()
    {
        var p = QPolicy.Empty();
        var learner = RlHoldControl.Learner(p, new RlOptions { LearningRate = 0.5 }, new Random(1), epsilon: 0);
        var bus = new TwinBus { Load = 10 };
        var config = new TwinConfig { RouteLengthMeters = 5000 };

        // prima decizie: lipit de cel din față; a doua: headway mult peste medie => recompensă negativă
        learner.ExtraHoldSeconds(new TwinStopContext(bus, 0, 100, 5000, 3000, 0, config, 300, 300));
        Assert.All(p.Q.SelectMany(r => r), v => Assert.Equal(0, v));
        learner.ExtraHoldSeconds(new TwinStopContext(bus, 0, 3000, 3000, 3000, 60, config, 900, 300));

        var first = p.State(100 / 3000.0, 5000 / 3000.0);
        Assert.True(p.Q[first][0] < 0, "o decizie urmată de un headway neregulat trebuie penalizată");
        Assert.Equal(1, p.Visits[first].Sum());
    }

    [Fact]
    public void ExcessWaitCost_IsZeroAtTheMeanHeadway_AndGrowsQuadratically()
    {
        var config = new TwinConfig { RouteLengthMeters = 5000, PassengerArrivalRate = 0.01 };
        TwinStopContext At(double h) => new(new TwinBus(), 0, 1, 1, 1, 0, config, h, 300);
        Assert.Equal(0, RlHoldControl.ExcessWaitCost(At(300)));
        Assert.Equal(0.01 * 100 * 100 / 2, RlHoldControl.ExcessWaitCost(At(400)), 6);
        Assert.Equal(4 * RlHoldControl.ExcessWaitCost(At(400)), RlHoldControl.ExcessWaitCost(At(500)), 6);
    }

    [Fact]
    public void TrainingIsDeterministicForASeed()
    {
        var a = RlTrainer.Train(15, seed: 7);
        var b = RlTrainer.Train(15, seed: 7);
        Assert.Equal(a.ToJson(), b.ToJson());
        Assert.NotEqual(a.ToJson(), RlTrainer.Train(15, seed: 8).ToJson());
    }

    [Fact]
    public void ThePolicyRoundTripsThroughJson()
    {
        var policy = RlTrainer.Train(5, seed: 3);
        var copy = QPolicy.FromJson(policy.ToJson());
        Assert.Equal(policy.Q.SelectMany(r => r), copy.Q.SelectMany(r => r));
        Assert.Equal(5, copy.Info.Episodes);
    }

    [Fact]
    public void TheShippedPolicyIsTrained_AndUsedByTheRlStrategy()
    {
        var policy = QPolicy.Default;
        Assert.Equal(64, policy.Q.Length);
        Assert.True(policy.Info.Episodes >= 1000);
        Assert.True(policy.Visits.Sum(v => v.Sum()) > 100_000);
        Assert.Contains(HoldPolicy.Reinforcement, HoldPolicy.Strategies);
        Assert.IsType<RlHoldControl>(HoldPolicy.Create(HoldPolicy.Reinforcement, new HoldOptions()));
    }

    [Fact]
    public void TheShippedPolicy_ReducesDelayOnHeldOutScenarios()
    {
        // seed-uri de test (nefolosite la antrenare sau validare)
        double none = 0, rl = 0;
        for (var seed = RlTrainer.EvaluationSeedBase; seed < RlTrainer.EvaluationSeedBase + 3; seed++)
        {
            foreach (var control in new ITwinControl[] { new NoControl(), RlHoldControl.Greedy(QPolicy.Default) })
            {
                var config = new TwinConfig { RouteLengthMeters = 5000 };
                var engine = new TwinEngine(config, Enumerable.Range(0, 4).Select(i => i * config.LoopLengthMeters / 4).ToList(),
                    seed, new[] { new TwinDisturbance(600, 0, 300) }, control);
                for (var t = 0; t < 4 * 3600; t++) engine.Step();
                if (control is NoControl) none += engine.TotalDelayPerPassengerSeconds; else rl += engine.TotalDelayPerPassengerSeconds;
            }
        }
        Assert.True(rl < 0.7 * none, $"fără control {none / 3:F0} s, RL {rl / 3:F0} s");
    }
}

public class TwinExperimentTests
{
    [Fact]
    public void ReportsEveryStrategyOnEveryScenario_WithPairedDifferencesAgainstHolding()
    {
        var report = TwinExperiment.Run(replications: 2, durationSeconds: 1800);

        Assert.Equal(TwinExperiment.Scenarios.Count, report.Scenarios.Count);
        foreach (var s in report.Scenarios)
        {
            Assert.Equal(HoldPolicy.Strategies.OrderBy(x => x), s.Strategies.Select(x => x.Strategy).OrderBy(x => x));
            Assert.Equal(HoldPolicy.Strategies.Count - 1, s.VersusHeadway.Count);
            Assert.All(s.Strategies, r => Assert.True(r.TotalDelay.Mean > 0));
        }
        var markdown = report.ToMarkdown();
        Assert.Contains("întârziere / pasager", markdown);
        Assert.Contains("rl − headway", markdown);
        Assert.Contains("\"versusHeadway\"", report.ToJson());
    }

    private static TwinRunResult Run(double delay) =>
        new(0, "even", 3, 60, 1000, 0, 0, 0, 1, 0, Array.Empty<TwinSample>(), Array.Empty<double>(), TotalDelayPerPassengerSeconds: delay);

    [Fact]
    public void PairedDifference_HasTheRightMeanInterval_AndCounts()
    {
        var a = new[] { 10.0, 12, 9, 11 }.Select(Run).ToList();
        var b = new[] { 12.0, 13, 12, 12 }.Select(Run).ToList();
        var d = TwinExperiment.Paired("x", a, b);              // diferențe: -2, -1, -3, -1

        Assert.Equal(-1.75, d.MeanDifference, 6);
        Assert.Equal(4, d.Better);
        Assert.Equal(0, d.Worse);
        // abaterea standard a diferențelor = 0,957; t(3) = 3,182; jumătatea intervalului = 3,182 * 0,957 / 2 = 1,52
        Assert.Equal(-1.75 - 1.52, d.CiLow, 1);
        Assert.Equal(-1.75 + 1.52, d.CiHigh, 1);
    }

    [Fact]
    public void TCritical_MatchesTheStudentTable() =>
        Assert.Equal((12.706, 2.093, 1.96), (TwinExperiment.TCritical95(1), TwinExperiment.TCritical95(19), TwinExperiment.TCritical95(500)));
}
