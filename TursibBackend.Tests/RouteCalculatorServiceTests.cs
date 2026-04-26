using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using Xunit;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend.Tests;

/// <summary>
/// Tests for RouteCalculatorService using an EF Core in-memory database.
/// Station coordinates use realistic Sibiu GPS values so Haversine distances
/// produce sensible walking / travel times.
/// </summary>
public class RouteCalculatorServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly RouteCalculatorService _sut;

    public RouteCalculatorServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated per test class instance
            .Options;
        _db  = new ApplicationDbContext(options);
        _sut = new RouteCalculatorService(_db, new MemoryCache(new MemoryCacheOptions()));
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Station MakeStation(int id, string name, double lat, double lon) =>
        new() { Id = id, Name = name, Latitude = lat, Longitude = lon };

    private static RouteModel MakeRoute(int id, string number, string name) =>
        new() { Id = id, RouteNumber = number, Name = name };

    private async Task SeedLinearRoute(
        int routeId, string routeNumber,
        IReadOnlyList<Station> stations)
    {
        _db.Routes.Add(MakeRoute(routeId, routeNumber, $"Route {routeNumber}"));
        _db.Stations.AddRange(stations);
        for (int i = 0; i < stations.Count; i++)
        {
            _db.RouteStations.Add(new RouteStation
            {
                Id        = routeId * 1000 + i,
                RouteId   = routeId,
                StationId = stations[i].Id,
                Order     = i + 1
            });
        }
        await _db.SaveChangesAsync();
    }

    // ── empty database ────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateAlternativeRoutes_ReturnsEmpty_WhenDatabaseIsEmpty()
    {
        var routes = await _sut.CalculateAlternativeRoutes(1, 2);
        Assert.Empty(routes);
    }

    [Fact]
    public async Task CalculateOptimalRoute_ReturnsNull_WhenDatabaseIsEmpty()
    {
        var route = await _sut.CalculateOptimalRoute(1, 2);
        Assert.Null(route);
    }

    // ── direct route ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateOptimalRoute_FindsDirectRoute_BetweenAdjacentStations()
    {
        // A → B on route 1 (two consecutive stations, ~0.5 km apart)
        var stationA = MakeStation(1, "A", 45.79, 24.14);
        var stationB = MakeStation(2, "B", 45.795, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var result = await _sut.CalculateOptimalRoute(1, 2);

        Assert.NotNull(result);
        Assert.Single(result!.Segments);
        Assert.Equal("bus", result.Segments[0].Type);
        Assert.Equal("direct", result.RouteType);
    }

    [Fact]
    public async Task CalculateOptimalRoute_FindsRouteAcrossMultipleStops()
    {
        // A → B → C → D on a single bus line
        var stations = new[]
        {
            MakeStation(10, "A", 45.76, 24.13),
            MakeStation(11, "B", 45.77, 24.13),
            MakeStation(12, "C", 45.78, 24.13),
            MakeStation(13, "D", 45.79, 24.13)
        };
        await SeedLinearRoute(1, "1", stations);

        var result = await _sut.CalculateOptimalRoute(10, 13);

        Assert.NotNull(result);
        Assert.Equal("direct", result!.RouteType);
        Assert.Equal("A", result.Segments[0].StartStation?.Name);
        Assert.Equal("D", result.Segments[0].EndStation?.Name);
    }

    // ── transfer route ────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateOptimalRoute_FindsTransferRoute_ViaSharedStation()
    {
        // Route 1: A → B → C
        // Route 2: C → D → E
        // Trip from A to E requires a transfer at C.
        var stationA = MakeStation(20, "A", 45.76, 24.13);
        var stationB = MakeStation(21, "B", 45.77, 24.13);
        var stationC = MakeStation(22, "C", 45.78, 24.13); // transfer point
        var stationD = MakeStation(23, "D", 45.79, 24.13);
        var stationE = MakeStation(24, "E", 45.80, 24.13);

        await SeedLinearRoute(1, "1", new[] { stationA, stationB, stationC });
        _db.Routes.Add(MakeRoute(2, "2", "Route 2"));
        _db.RouteStations.AddRange(
            new RouteStation { Id = 2001, RouteId = 2, StationId = 22, Order = 1 },
            new RouteStation { Id = 2002, RouteId = 2, StationId = 23, Order = 2 },
            new RouteStation { Id = 2003, RouteId = 2, StationId = 24, Order = 3 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.CalculateOptimalRoute(20, 24);

        Assert.NotNull(result);
        // Must have at least two bus segments (one per line)
        var busSegments = result!.Segments.Where(s => s.Type == "bus").ToList();
        Assert.True(busSegments.Count >= 2, "Expected at least 2 bus segments for a transfer route");
    }

    // ── unknown stations ──────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateOptimalRoute_ReturnsNull_ForUnknownStartStation()
    {
        var stationA = MakeStation(30, "A", 45.79, 24.14);
        var stationB = MakeStation(31, "B", 45.80, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var result = await _sut.CalculateOptimalRoute(999, 31);

        Assert.Null(result);
    }

    [Fact]
    public async Task CalculateOptimalRoute_ReturnsNull_ForUnknownEndStation()
    {
        var stationA = MakeStation(40, "A", 45.79, 24.14);
        var stationB = MakeStation(41, "B", 45.80, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var result = await _sut.CalculateOptimalRoute(40, 999);

        Assert.Null(result);
    }

    // ── timestamps ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateAlternativeRoutes_StampsSegmentTimesCorrectly()
    {
        var stationA = MakeStation(50, "A", 45.79, 24.14);
        var stationB = MakeStation(51, "B", 45.80, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var departure = new DateTime(2026, 4, 25, 8, 0, 0, DateTimeKind.Local);
        var routes = await _sut.CalculateAlternativeRoutes(50, 51, departureTime: departure);

        Assert.NotEmpty(routes);
        var first = routes[0];
        Assert.Equal(departure, first.DepartureTime);
        Assert.True(first.ArrivalTime > first.DepartureTime);
        // Each segment has start/end times set
        foreach (var seg in first.Segments)
        {
            Assert.True(seg.EndTime >= seg.StartTime);
        }
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_ArriveBy_SetsCorrectDepartureTime()
    {
        var stationA = MakeStation(60, "A", 45.79, 24.14);
        var stationB = MakeStation(61, "B", 45.80, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var arriveBy = new DateTime(2026, 4, 25, 9, 0, 0, DateTimeKind.Local);
        var routes = await _sut.CalculateAlternativeRoutes(60, 61, arrivalTime: arriveBy);

        Assert.NotEmpty(routes);
        // ArrivalTime should equal (or be very close to) the requested arriveBy
        var first = routes[0];
        Assert.True(first.ArrivalTime <= arriveBy.AddSeconds(1));
        Assert.True(first.DepartureTime < first.ArrivalTime);
    }

    // ── route ranking and deduplication ───────────────────────────────────────

    [Fact]
    public async Task CalculateAlternativeRoutes_ReturnsAtMostThreeRoutes()
    {
        var stationA = MakeStation(70, "A", 45.79, 24.14);
        var stationB = MakeStation(71, "B", 45.80, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var routes = await _sut.CalculateAlternativeRoutes(70, 71);

        Assert.True(routes.Count <= 3);
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_AssignsSequentialRankings()
    {
        var stationA = MakeStation(80, "A", 45.79, 24.14);
        var stationB = MakeStation(81, "B", 45.80, 24.14);
        await SeedLinearRoute(1, "1", new[] { stationA, stationB });

        var routes = await _sut.CalculateAlternativeRoutes(80, 81);

        for (int i = 0; i < routes.Count; i++)
            Assert.Equal(i + 1, routes[i].RouteRank);
    }

    // ── walking ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateOptimalRoute_UsesWalkingEdge_BetweenNearbyStations()
    {
        // Route 1: A → B (far end)
        // Route 2: C → D  where C is within 500 m of B, enabling a walk-transfer
        var stationA = MakeStation(90, "A", 45.76, 24.13);
        var stationB = MakeStation(91, "B", 45.80, 24.14);
        // C is 0.3 km from B — within the 500 m walking threshold
        var stationC = MakeStation(92, "C", 45.8025, 24.14);
        var stationD = MakeStation(93, "D", 45.81, 24.14);

        await SeedLinearRoute(1, "1", new[] { stationA, stationB });
        _db.Routes.Add(MakeRoute(2, "2", "Route 2"));
        _db.RouteStations.AddRange(
            new RouteStation { Id = 3001, RouteId = 2, StationId = 92, Order = 1 },
            new RouteStation { Id = 3002, RouteId = 2, StationId = 93, Order = 2 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.CalculateOptimalRoute(90, 93);

        // A route should be found even though there is no direct connection
        Assert.NotNull(result);
        var hasWalk = result!.Segments.Any(s => s.Type == "walk");
        Assert.True(hasWalk, "Expected at least one walking segment");
    }
}
