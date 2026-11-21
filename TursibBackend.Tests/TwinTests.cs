using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class TwinEngineTests
{
    private const double Loop = 10_000;
    private static List<double> Even(int n) => Enumerable.Range(0, n).Select(i => i * Loop / n).ToList();
    private static TwinScenario Scenario(int seconds = 14_400, int seed = 1) => new() { RouteId = 1, DurationSeconds = seconds, Seed = seed };
    private static TwinConfig Config(double dwellJitter = 0.3) => new() { RouteLengthMeters = Loop / 2, DwellJitter = dwellJitter };

    [Fact]
    public void SameSeed_GivesIdenticalResults()
    {
        var a = TwinRunner.Run(Config(), Even(3), Scenario(seed: 7), "even");
        var b = TwinRunner.Run(Config(), Even(3), Scenario(seed: 7), "even");
        Assert.Equal(a.MeanCv, b.MeanCv);
        Assert.Equal(a.FinalLoopPositions, b.FinalLoopPositions);
        Assert.Equal(a.Series.Count, b.Series.Count);
    }

    [Fact]
    public void EvenFleetWithoutNoise_StaysEvenlySpaced()
    {
        var r = TwinRunner.Run(Config(dwellJitter: 0), Even(3), Scenario(), "even");
        Assert.Equal(0, r.BunchedPercent);
        Assert.True(r.MeanCv < 0.05, $"CV {r.MeanCv}");
        Assert.Equal(0, r.BunchingEpisodes);
    }

    [Fact]
    public void LongStopOfOneBus_CausesBunching()
    {
        var scenario = Scenario();
        scenario.Disturbances.Add(new TwinDisturbance(AtSecond: 600, BusIndex: 0, HoldSeconds: 240));

        var r = TwinRunner.Run(Config(dwellJitter: 0), Even(3), scenario, "even");

        Assert.True(r.BunchedPercent > 50, $"bunched {r.BunchedPercent}%");
        Assert.True(r.BunchingEpisodes >= 1);
        Assert.True(r.MinRatio < 0.1);
    }

    [Fact]
    public void Buses_NeverOvertakeOrOverlap()
    {
        var scenario = Scenario();
        scenario.Disturbances.Add(new TwinDisturbance(300, 1, 600));
        var config = Config();
        var engine = new TwinEngine(config, Even(4), 3, scenario.Disturbances);

        for (var t = 0; t < 10_000; t++)
        {
            engine.Step();
            var positions = engine.Buses.Select(engine.LoopPosition).OrderBy(p => p).ToList();
            for (var i = 0; i < positions.Count; i++)
            {
                var gap = (positions[(i + 1) % positions.Count] - positions[i] + Loop) % Loop;
                Assert.True(gap >= config.MinGapMeters - 1, $"t={t}: gap {gap}");
            }
        }
    }

    private sealed class ConstantHold(double seconds) : ITwinControl
    {
        public double ExtraHoldSeconds(TwinStopContext context) => seconds;
    }

    [Fact]
    public void ControlHook_CanHoldBusesAtStops()
    {
        var free = new TwinEngine(Config(0), Even(3), 1);
        var held = new TwinEngine(Config(0), Even(3), 1, control: new ConstantHold(30));
        for (var t = 0; t < 3000; t++) { free.Step(); held.Step(); }

        Assert.True(held.Buses.Sum(b => b.Position) < free.Buses.Sum(b => b.Position) * 0.8,
            "holding la stații trebuie să încetinească flota");
    }

    [Fact]
    public void Control_ReceivesGapsAroundTheBus()
    {
        var seen = new List<TwinStopContext>();
        var engine = new TwinEngine(Config(0), Even(3), 1, control: new Recorder(seen));
        for (var t = 0; t < 600; t++) engine.Step();

        Assert.NotEmpty(seen);
        Assert.All(seen, c => Assert.Equal(Loop / 3, c.IdealGapMeters, 3));
        Assert.All(seen, c => Assert.InRange(c.GapAheadMeters, 1, Loop));
    }

    private sealed class Recorder(List<TwinStopContext> sink) : ITwinControl
    {
        public double ExtraHoldSeconds(TwinStopContext context) { sink.Add(context); return 0; }
    }
}

public class HoldPolicyTests
{
    private static readonly HoldOptions O = new() { TriggerRatio = 0.85, MaxHoldSeconds = 45, MinSpeedMs = 4 };

    [Fact]
    public void Headway_HoldsLongEnoughToRecoverTheMissingGap()
    {
        // ideal 3000 m, gol 2400 m, 10 m/s: lipsesc 600 m => 60 s, plafonat la 45 s
        Assert.Equal(45, HoldPolicy.HeadwayHold(2400, 3000, 10, O));
        // lipsesc 300 m => 30 s
        Assert.Equal(30, HoldPolicy.HeadwayHold(2700, 3000, 10, new HoldOptions { TriggerRatio = 0.95, MaxHoldSeconds = 45 }));
    }

    [Fact]
    public void NoHold_WhenGapIsAboveTheTrigger()
    {
        Assert.Equal(0, HoldPolicy.HeadwayHold(2600, 3000, 10, O));   // 0,87 ≥ 0,85
        Assert.Equal(0, HoldPolicy.EqualizeHold(2600, 5000, 3000, 10, O));
    }

    [Fact]
    public void Equalize_SplitsTheDifferenceBetweenFrontAndBackGaps()
    {
        // față 1000, spate 2000, 10 m/s: după h secunde față=1000+10h, spate=2000-10h => egale la h=50 => plafon 45
        Assert.Equal(45, HoldPolicy.EqualizeHold(1000, 2000, 3000, 10, O));
        // față 1000, spate 1400 => h = 400 / 20 = 20 s
        Assert.Equal(20, HoldPolicy.EqualizeHold(1000, 1400, 3000, 10, O));
    }

    [Fact]
    public void Equalize_DoesNotHold_WhenTheBusBehindIsAlreadyCloser()
    {
        Assert.Equal(0, HoldPolicy.EqualizeHold(1000, 600, 3000, 10, O));
    }

    [Fact]
    public void SlowOrStoppedBus_UsesTheMinimumSpeed()
    {
        // 0 m/s => se folosește 4 m/s: deficit 400 m => 100 s, plafonat
        Assert.Equal(45, HoldPolicy.HeadwayHold(1000, 1400, 0, new HoldOptions { TriggerRatio = 1, MaxHoldSeconds = 45 }));
    }

    [Fact]
    public void Strategies_AreKnownAndFactoryFallsBackToNoControl()
    {
        Assert.True(HoldPolicy.IsKnown("headway"));
        Assert.False(HoldPolicy.IsKnown("magic"));
        Assert.IsType<NoControl>(HoldPolicy.Create("none", O));
        Assert.IsType<HeadwayHoldControl>(HoldPolicy.Create("headway", O));
        Assert.IsType<EqualizeHoldControl>(HoldPolicy.Create("equalize", O));
    }

    [Fact]
    public void Analyzer_AddsARecommendation_WhenHoldOptionsAreGiven()
    {
        var buses = new[] { new BusOnLoop("a", 1000, 36), new BusOnLoop("b", 1050, 36), new BusOnLoop("c", 5000, 36) };

        var pairs = BunchingAnalyzer.ConsecutiveGaps(1, "1", buses, 9000, O);

        Assert.Equal(45, pairs.Single(p => p.FollowerId == "a").RecommendedHoldSeconds);   // a e lipit de b
        Assert.Equal(0, pairs.Single(p => p.FollowerId == "c").RecommendedHoldSeconds);    // c are loc în față
        Assert.All(BunchingAnalyzer.ConsecutiveGaps(1, "1", buses, 9000), p => Assert.Null(p.RecommendedHoldSeconds));
    }
}

public class TwinStrategyTests
{
    private const double Loop = 10_000;
    private static List<double> Even(int n) => Enumerable.Range(0, n).Select(i => i * Loop / n).ToList();

    [Theory]
    [InlineData(HoldPolicy.Headway)]
    [InlineData(HoldPolicy.Equalize)]
    public void Control_PreventsBunchingAfterADisturbance(string strategy)
    {
        var holds = new HoldOptions();
        double none = 0, controlled = 0;
        for (var seed = 1; seed <= 4; seed++)
        {
            var scenario = new TwinScenario { RouteId = 1, DurationSeconds = 14_400, Seed = seed };
            scenario.Disturbances.Add(new TwinDisturbance(600, 0, 300));
            var config = new TwinConfig { RouteLengthMeters = Loop / 2 };
            none += TwinRunner.Run(config, Even(4), scenario, "even").BunchedPercent;
            var r = TwinRunner.Run(config, Even(4), scenario, "even", control: HoldPolicy.Create(strategy, holds), strategy: strategy);
            controlled += r.BunchedPercent;
            Assert.Equal(strategy, r.Strategy);
            Assert.True(r.HoldSecondsPerBusHour > 0, "controlul are un cost măsurabil");
        }
        Assert.True(none / 4 > 50, $"fără control: {none / 4}%");
        Assert.True(controlled / 4 < 5, $"cu {strategy}: {controlled / 4}%");
    }

    [Fact]
    public void NoControl_HasZeroHoldCost()
    {
        var r = TwinRunner.Run(new TwinConfig { RouteLengthMeters = Loop / 2 }, Even(3), new TwinScenario { RouteId = 1, DurationSeconds = 3600 }, "even");
        Assert.Equal(0, r.HoldSecondsPerBusHour);
        Assert.InRange(r.AverageSpeedKmh, 10, 30);
    }
}

public class TwinScenarioTests
{
    [Fact]
    public void Validate_AcceptsDefaultsAndRejectsBadInput()
    {
        Assert.Null(new TwinScenario { RouteId = 1 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 0 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, DurationSeconds = 10 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, DurationSeconds = TwinScenario.MaxDurationSeconds + 1 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, BusCount = 1 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, BusCount = 99 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, DemandFactor = 50 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, Strategy = "magic" }.Validate());
        Assert.Null(new TwinScenario { RouteId = 1, Strategy = "equalize" }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, Disturbances = { new TwinDisturbance(9000, 0, 60) } }.Validate());
    }
}

public class TwinServiceTests
{
    private sealed class FixedGeometry(RouteGeometry? geometry) : IRouteGeometryProvider
    {
        public Task<RouteGeometry?> GetAsync(int routeId, CancellationToken ct) => Task.FromResult(geometry);
    }

    // traseu nord-sud de ~5 km
    private static RouteGeometry Geometry() => RouteGeometry.FromPoints(
        Enumerable.Range(0, 11).Select(i => (45.75 + i * 0.004522, 24.15)).ToList())!;

    private static BusPosition Live(int i, double alongMeters, int direction = 0) => new()
    {
        Id = $"route_5_bus_{i}", RouteId = 5, DirectionId = direction,
        Latitude = 45.75 + alongMeters / 110_574.0, Longitude = 24.15
    };

    [Fact]
    public async Task ReturnsNull_WhenRouteHasNoGeometry()
    {
        var service = new TwinService(new FixedGeometry(null), new LiveBusState(), new BunchingOptions(), new HoldOptions());
        Assert.Null(await service.RunAsync(new TwinScenario { RouteId = 5 }, default));
    }

    [Fact]
    public async Task StartsFromLivePositions_WhenRequestedAndAvailable()
    {
        var state = new LiveBusState();
        // două autobuze lipite (dus) și unul în sens invers: twin-ul trebuie să pornă deja „bunched”
        state.Apply(new[] { Live(0, 1000), Live(1, 1100), Live(2, 2000, direction: 1) });
        var service = new TwinService(new FixedGeometry(Geometry()), state, new BunchingOptions(), new HoldOptions());

        var r = await service.RunAsync(new TwinScenario { RouteId = 5, UseLiveState = true, DurationSeconds = 600 }, default);

        Assert.NotNull(r);
        Assert.Equal("live", r!.InitialisedFrom);
        Assert.Equal(3, r.BusCount);
        Assert.True(r.Series[0].MinRatio < 0.1, "pornește de la bunching-ul real");
    }

    [Fact]
    public async Task FallsBackToEvenSpacing_WhenNoLiveBuses()
    {
        var service = new TwinService(new FixedGeometry(Geometry()), new LiveBusState(), new BunchingOptions(), new HoldOptions());

        var r = await service.RunAsync(new TwinScenario { RouteId = 5, UseLiveState = true, BusCount = 4, DurationSeconds = 600 }, default);

        Assert.Equal("even", r!.InitialisedFrom);
        Assert.Equal(4, r.BusCount);
        Assert.True(r.Series[0].MinRatio > 0.9);
    }
}

public class TwinCompareRequestTests
{
    [Fact]
    public void Validate_ChecksStrategiesAndReplications()
    {
        Assert.Null(new TwinCompareRequest { RouteId = 1 }.Validate());
        Assert.NotNull(new TwinCompareRequest { RouteId = 1, Replications = 0 }.Validate());
        Assert.NotNull(new TwinCompareRequest { RouteId = 1, Replications = 99 }.Validate());
        Assert.NotNull(new TwinCompareRequest { RouteId = 1, Strategies = new() { "nope" } }.Validate());
        Assert.NotNull(new TwinCompareRequest { RouteId = 1, Strategies = new() }.Validate());
        Assert.NotNull(new TwinCompareRequest { RouteId = 0 }.Validate());   // validarea de bază rămâne activă
    }
}

public class TwinEndpointTests : IClassFixture<TwinEndpointTests.TwinFactory>
{
    public class TwinFactory : AuthWebAppFactory { }

    private readonly TwinFactory _factory;

    public TwinEndpointTests(TwinFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Trips.Any(t => t.RouteId == 9)) return;
        db.Trips.Add(new Trip { TripId = "t9", RouteId = 9, ShapeId = "shape9", DirectionId = 0 });
        db.ShapePoints.AddRange(Enumerable.Range(0, 11).Select(i =>
            new ShapePoint { ShapeId = "shape9", Latitude = 45.75 + i * 0.004522, Longitude = 24.15, Sequence = i }));
        db.SaveChanges();
    }

    [Fact]
    public async Task Run_ReturnsMetrics_AndAppliesTheDisturbance()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/twin/run", new
        {
            routeId = 9, busCount = 3, durationSeconds = 7200, seed = 1,
            disturbances = new[] { new { atSecond = 600, busIndex = 0, holdSeconds = 300 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("even", body.GetProperty("initialisedFrom").GetString());
        Assert.Equal(3, body.GetProperty("busCount").GetInt32());
        Assert.InRange(body.GetProperty("routeLengthMeters").GetDouble(), 4900, 5100);
        Assert.True(body.GetProperty("series").GetArrayLength() > 100);
        Assert.True(body.GetProperty("bunchedPercent").GetDouble() > 0);
    }

    [Fact]
    public async Task Compare_ReturnsOneSummaryPerStrategy_AndControlBeatsNoControl()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/twin/compare", new
        {
            routeId = 9, busCount = 4, durationSeconds = 14400, seed = 1, replications = 3,
            disturbances = new[] { new { atSecond = 600, busIndex = 0, holdSeconds = 300 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("replications").GetInt32());
        var results = body.GetProperty("results").EnumerateArray()
            .ToDictionary(r => r.GetProperty("strategy").GetString()!, r => r);
        Assert.Equal(new[] { "equalize", "headway", "none", "rl" }, results.Keys.Order());

        var none = results["none"].GetProperty("bunchedPercent").GetDouble();
        Assert.True(none > 50, $"fără control {none}%");
        Assert.Equal(0, results["none"].GetProperty("holdSecondsPerBusHour").GetDouble());
        foreach (var strategy in new[] { "headway", "equalize", "rl" })
        {
            Assert.True(results[strategy].GetProperty("bunchedPercent").GetDouble() < none / 4, strategy);
            Assert.True(results[strategy].GetProperty("holdSecondsPerBusHour").GetDouble() > 0, strategy);
            Assert.True(results[strategy].GetProperty("series").GetArrayLength() > 100);
        }
    }

    [Fact]
    public async Task Compare_Returns400_ForUnknownStrategy()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/twin/compare",
            new { routeId = 9, strategies = new[] { "magic" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Run_AppliesTheRequestedStrategy()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/twin/run",
            new { routeId = 9, busCount = 3, durationSeconds = 3600, strategy = "headway" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("headway", body.GetProperty("strategy").GetString());
    }

    [Fact]
    public async Task Run_Returns400_ForInvalidScenario()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/twin/run", new { routeId = 9, busCount = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Run_Returns404_ForRouteWithoutShape()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/twin/run", new { routeId = 12345 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
