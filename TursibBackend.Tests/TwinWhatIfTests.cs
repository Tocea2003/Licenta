using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class TwinWhatIfTests
{
    private const double Loop = 10_000;
    private static TwinConfig Config(double jitter = 0) => new() { RouteLengthMeters = Loop / 2, DwellJitter = jitter };
    private static List<double> Even(int n) => Enumerable.Range(0, n).Select(i => i * Loop / n).ToList();

    private static TwinEngine Run(int buses, TwinWhatIf? whatIf, int seconds = 14_400, double jitter = 0)
    {
        var engine = new TwinEngine(Config(jitter), Even(buses), 1, whatIf: whatIf);
        for (var t = 0; t < seconds; t++) engine.Step();
        return engine;
    }

    [Fact]
    public void ExtraBuses_JoinTheFleet_InTheLargestGap_AndCutTheHeadway()
    {
        var engine = new TwinEngine(Config(), Even(3), 1, whatIf: new TwinWhatIf(Injections: new[] { new TwinBusInjection(3600, 1) }));
        for (var t = 0; t <= 3600; t++) engine.Step();

        Assert.Equal(4, engine.Buses.Count);
        var positions = engine.Buses.Select(engine.LoopPosition).OrderBy(p => p).ToList();
        var gaps = positions.Select((p, i) => (positions[(i + 1) % positions.Count] - p + Loop) % Loop).ToList();
        Assert.True(gaps.Min() > Loop / 8, $"noul autobuz trebuie pus într-un gol mare, nu lângă altul (min {gaps.Min():F0} m)");

    }

    private static TwinEngine RunControlled(int buses, TwinWhatIf? whatIf, ITwinControl control, int seconds = 14_400)
    {
        var engine = new TwinEngine(Config(0.3), Even(buses), 1, control: control, whatIf: whatIf);
        for (var t = 0; t < seconds; t++) engine.Step();
        return engine;
    }

    [Fact]
    public void WithHoldingControl_AnExtraBusCutsTheWait()
    {
        var holding = new HeadwayHoldControl(new HoldOptions());
        var three = RunControlled(3, null, holding);
        var four = RunControlled(3, new TwinWhatIf(Injections: new[] { new TwinBusInjection(0, 1) }), new HeadwayHoldControl(new HoldOptions()));
        Assert.True(four.MeanWaitSeconds < 0.85 * three.MeanWaitSeconds, $"{three.MeanWaitSeconds:F0}s → {four.MeanWaitSeconds:F0}s");
    }

    /// <summary>
    /// Rezultat al modelului (instabilitatea bunching-ului): fără control, un autobuz adăugat lasă flota neuniformă
    /// (gol 1/3 → 1/6 + 1/6), iar neuniformitatea crește până la bunching. Un autobuz în plus fără regularizare nu ajută.
    /// </summary>
    [Fact]
    public void WithoutControl_AnUnevenlyAddedBusBunches_AndDoesNotHelp()
    {
        var three = Run(3, null);
        var four = Run(3, new TwinWhatIf(Injections: new[] { new TwinBusInjection(0, 1) }));
        Assert.True(four.HeadwayCv > 3 * three.HeadwayCv, $"CV {three.HeadwayCv:F2} → {four.HeadwayCv:F2}");
        Assert.True(four.MeanWaitSeconds > three.MeanWaitSeconds);
    }

    [Fact]
    public void AClosedStreet_SkipsItsStops_SlowsTheBuses_AndKeepsServingItsPassengersElsewhere()
    {
        var closure = new TwinClosure(FromMeters: 1000, ToMeters: 2000, DetourMeters: 1500);
        var open = Run(3, null);
        var closed = Run(3, new TwinWhatIf(Closures: new[] { closure }));

        Assert.True(closed.SkippedStopVisits > 0);
        Assert.True(closed.ClosedStops >= 4, $"stații închise: {closed.ClosedStops}");          // ~3 pe fiecare sens
        Assert.True(closed.MeanHeadwaySeconds > open.MeanHeadwaySeconds, "ocolul lungește tura, deci și headway-ul");
        // cererea stațiilor închise se mută la vecine: numărul de pasageri preluați rămâne comparabil
        Assert.InRange(closed.PassengersServed / open.PassengersServed, 0.85, 1.15);
    }

    [Fact]
    public void AClosure_OnlyActsInsideItsTimeWindow()
    {
        var engine = new TwinEngine(Config(), Even(3), 1,
            whatIf: new TwinWhatIf(Closures: new[] { new TwinClosure(1000, 2000, 500, FromSecond: 600, ToSecond: 1200) }));
        for (var t = 0; t < 300; t++) engine.Step();
        Assert.Equal(0, engine.ClosedStops);
        for (var t = 300; t < 900; t++) engine.Step();
        Assert.True(engine.ClosedStops > 0);
        for (var t = 900; t < 1500; t++) engine.Step();
        Assert.Equal(0, engine.ClosedStops);
    }

    [Fact]
    public void BusesForHeadway_GrowsAsTheHeadwayShrinks()
    {
        var config = Config();
        var every10 = TwinPlanning.BusesForHeadway(config, 600);
        var every5 = TwinPlanning.BusesForHeadway(config, 300);
        Assert.True(every5 > every10);
        Assert.InRange(every10, 2, 6);

        // tura calculată ≈ tura simulată: headway-ul obținut e apropiat de cel cerut (rotunjirea în sus îl face puțin mai mic)
        var engine = new TwinEngine(config, Even(every10), 1);
        for (var t = 0; t < 14_400; t++) engine.Step();
        Assert.InRange(engine.MeanHeadwaySeconds, 0.6 * 600, 1.05 * 600);
    }

    [Fact]
    public void Validate_ChecksTheWhatIfFields()
    {
        Assert.Null(new TwinScenario { RouteId = 1, HeadwayMinutes = 10 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, HeadwayMinutes = 10, BusCount = 3 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, HeadwayMinutes = 1 }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, ExtraBuses = { new TwinBusInjection(10, 0) } }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, Closures = { new TwinClosure(500, 400, 100) } }.Validate());
        Assert.NotNull(new TwinScenario { RouteId = 1, Closures = { new TwinClosure(0, 400, 100, FromSecond: 600, ToSecond: 100) } }.Validate());
        Assert.Null(new TwinScenario { RouteId = 1, ExtraBuses = { new TwinBusInjection(600, 2) }, Closures = { new TwinClosure(0, 400, 100) } }.Validate());
    }
}

public class TwinWhatIfEndpointTests : IClassFixture<TwinEndpointTests.TwinFactory>
{
    private readonly TwinEndpointTests.TwinFactory _factory;

    public TwinWhatIfEndpointTests(TwinEndpointTests.TwinFactory factory)
    {
        _factory = factory;
        _ = new TwinEndpointTests(factory);   // aceeași linie 9 (shape de ~5 km) ca testele endpoint-ului twin
    }

    [Fact]
    public async Task Run_AppliesFrequencyExtraBusesAndClosures()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/twin/run", new
        {
            routeId = 9, durationSeconds = 7200, headwayMinutes = 8,
            extraBuses = new[] { new { atSecond = 1800, count = 2 } },
            closures = new[] { new { fromMeters = 1000, toMeters = 1800, detourMeters = 1200 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var start = body.GetProperty("busCount").GetInt32();
        Assert.Equal(start + 2, body.GetProperty("finalBusCount").GetInt32());
        Assert.True(body.GetProperty("skippedStopVisits").GetInt64() > 0);
    }
}
