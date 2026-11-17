using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TursibBackend.Controllers;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class RouteGeometryTests
{
    // Linie dreaptă nord-sud de ~5 km pe lon 24.15
    private static RouteGeometry Straight() => RouteGeometry.FromPoints(
        Enumerable.Range(0, 11).Select(i => (45.75 + i * 0.004522, 24.15)).ToList())!;

    [Fact]
    public void Length_IsSumOfSegments()
    {
        Assert.InRange(Straight().LengthMeters, 4950, 5050);
    }

    [Fact]
    public void Project_ReturnsDistanceAlongRoute_AndLateralOffset()
    {
        var g = Straight();
        // 1000 m pe traseu, la ~50 m lateral
        var (along, offset) = g.Project(45.75 + 1000 / 110_574.0, 24.15 + 50 / (111_320 * Math.Cos(45.75 * Math.PI / 180)));
        Assert.InRange(along, 990, 1010);
        Assert.InRange(offset, 45, 55);
    }

    [Fact]
    public void Project_UsesHintToStayOnTheRightPassOfALoopingRoute()
    {
        // traseu în „U” care revine pe același drum: a doua trecere e la același punct GPS
        var points = new List<(double, double)>();
        for (var i = 0; i <= 5; i++) points.Add((45.75 + i * 0.004522, 24.15));
        for (var i = 4; i >= 0; i--) points.Add((45.75 + i * 0.004522, 24.15));
        var g = RouteGeometry.FromPoints(points)!;

        var secondPass = g.Project(45.75 + 0.004522 * 2, 24.15, hintAlong: g.LengthMeters - 1000);
        Assert.True(secondPass.Along > g.LengthMeters / 2, $"era {secondPass.Along}");
    }

    [Fact]
    public void FromPoints_RejectsDegenerateShapes()
    {
        Assert.Null(RouteGeometry.FromPoints(new List<(double, double)> { (45.75, 24.15) }));
        Assert.Null(RouteGeometry.FromPoints(new List<(double, double)> { (45.75, 24.15), (45.7501, 24.15) }));
    }
}

public class BunchingAnalyzerTests
{
    [Theory]
    [InlineData(1000, 0, 5000, 1000)]   // dus: distanța parcursă
    [InlineData(1000, 1, 5000, 9000)]   // întors: 2L - distanța
    public void LoopPosition_ProgressesInDirectionOfTravel(double along, int dir, double length, double expected) =>
        Assert.Equal(expected, BunchingAnalyzer.LoopPosition(along, dir, length));

    [Fact]
    public void ConsecutiveGaps_AreCircularAndSumToTheLoop()
    {
        var buses = new[] { new BusOnLoop("a", 1000, 30), new BusOnLoop("b", 1100, 30), new BusOnLoop("c", 3000, 30) };
        var pairs = BunchingAnalyzer.ConsecutiveGaps(9, "9", buses, 10_000);

        Assert.Equal(3, pairs.Count);
        Assert.Equal(10_000, pairs.Sum(p => p.GapMeters), 3);
        var ab = pairs.Single(p => p.FollowerId == "a");
        Assert.Equal("b", ab.LeaderId);
        Assert.Equal(100, ab.GapMeters, 3);
        Assert.Equal(100 / (10_000 / 3.0), ab.Ratio, 3);
        var wrap = pairs.Single(p => p.FollowerId == "c");
        Assert.Equal("a", wrap.LeaderId);
        Assert.Equal(8000, wrap.GapMeters, 3);
    }

    [Fact]
    public void ConsecutiveGaps_NeedsAtLeastTwoBuses() =>
        Assert.Empty(BunchingAnalyzer.ConsecutiveGaps(1, "1", new[] { new BusOnLoop("a", 10, 20) }, 1000));
}

public class BunchingTrackerTests
{
    private static BunchingPair Pair(double ratio, string follower = "f") =>
        new(1, "1", "l", follower, ratio * 1000, 1000, ratio, 60);

    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(1_000_000);
    private static BunchingTracker Tracker(int minDuration = 10) =>
        new(new BunchingOptions { EnterRatio = 0.35, ExitRatio = 0.6, MinDurationSeconds = minDuration });

    [Fact]
    public void Alert_RequiresTheSituationToPersist()
    {
        var t = Tracker();
        Assert.Empty(t.Update(new[] { Pair(0.2) }, T0));
        Assert.Empty(t.Update(new[] { Pair(0.2) }, T0.AddSeconds(5)));
        var changes = t.Update(new[] { Pair(0.2) }, T0.AddSeconds(10));

        var started = Assert.Single(changes);
        Assert.True(started.Started);
        Assert.Equal("warning", started.Alert.Severity);
        Assert.Single(t.Active);
    }

    [Fact]
    public void ShortDip_DoesNotRaiseAnAlert()
    {
        var t = Tracker();
        t.Update(new[] { Pair(0.2) }, T0);
        t.Update(new[] { Pair(0.5) }, T0.AddSeconds(5));          // revine peste prag: resetăm
        Assert.Empty(t.Update(new[] { Pair(0.2) }, T0.AddSeconds(12)));
        Assert.Empty(t.Active);
    }

    [Fact]
    public void ActiveAlert_StaysOpenInsideHysteresisBand_AndClosesAboveExit()
    {
        var t = Tracker(0);
        t.Update(new[] { Pair(0.1) }, T0);
        Assert.Single(t.Active);
        Assert.Equal("critical", t.Active[0].Severity);

        Assert.Empty(t.Update(new[] { Pair(0.5) }, T0.AddSeconds(2)));   // între 0.35 și 0.6: rămâne
        Assert.Single(t.Active);

        var closed = Assert.Single(t.Update(new[] { Pair(0.7) }, T0.AddSeconds(4)));
        Assert.False(closed.Started);
        Assert.NotNull(closed.Alert.EndedAt);
        Assert.Empty(t.Active);
        Assert.Single(t.Recent);
    }

    [Fact]
    public void Alert_EndsWhenThePairDisappears()
    {
        var t = Tracker(0);
        t.Update(new[] { Pair(0.1) }, T0);
        var changes = t.Update(Array.Empty<BunchingPair>(), T0.AddSeconds(1));
        Assert.False(Assert.Single(changes).Started);
        Assert.Empty(t.Active);
    }
}

public class BunchingEndToEndTests : IClassFixture<BunchingEndToEndTests.BunchingFactory>
{
    public class BunchingFactory : AuthWebAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Ingest:ApiKey", "test-key");
            builder.UseSetting("Bunching:MinDurationSeconds", "0");
            builder.UseSetting("Bunching:EvaluationIntervalSeconds", "0");
        }
    }

    private readonly BunchingFactory _factory;
    public BunchingEndToEndTests(BunchingFactory factory)
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

    private static object Bus(int index, double alongMeters) => new
    {
        id = $"route_9_bus_{index}", routeId = 9, routeNumber = "9", directionId = 0, speed = 25,
        latitude = 45.75 + alongMeters / 110_574.0, longitude = 24.15
    };

    private HttpClient Ingest()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add(LiveController.ApiKeyHeader, "test-key");
        return c;
    }

    [Fact]
    public async Task BunchedBuses_RaiseAlert_ViaRestAndSignalR()
    {
        var pushed = new TaskCompletionSource<List<BunchingAlert>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/buses"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        hub.On<List<BunchingAlert>>("bunching", alerts => { if (alerts.Count > 0) pushed.TrySetResult(alerts); });
        await hub.StartAsync();

        try
        {
            // două autobuze lipite la ~1 km, al treilea departe
            var client = Ingest();
            var response = await client.PostAsJsonAsync("/api/live/positions",
                new[] { Bus(0, 1000), Bus(1, 1100), Bus(2, 3000) });
            response.EnsureSuccessStatusCode();

            var alerts = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var alert = Assert.Single(alerts);
            Assert.Equal("route_9_bus_0", alert.FollowerId);
            Assert.Equal("route_9_bus_1", alert.LeaderId);
            Assert.Equal("critical", alert.Severity);
            Assert.InRange(alert.GapMeters, 90, 110);

            var rest = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/live/bunching");
            Assert.Equal(1, rest.GetProperty("active").GetArrayLength()); // o singură alertă activă
            Assert.Single(await hub.InvokeAsync<List<BunchingAlert>>("GetBunching"));

            // se îndepărtează: alerta se închide
            await client.PostAsJsonAsync("/api/live/positions", new[] { Bus(0, 500), Bus(1, 3000), Bus(2, 4900) });
            for (var i = 0; i < 50; i++)
            {
                var now = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/live/bunching");
                if (now.GetProperty("active").GetArrayLength() == 0) break;
                await Task.Delay(100);
            }
            var after = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/live/bunching");
            Assert.Equal(0, after.GetProperty("active").GetArrayLength());
            Assert.Equal(1, after.GetProperty("recent").GetArrayLength());
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }
}
