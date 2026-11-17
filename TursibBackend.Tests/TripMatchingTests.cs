using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TransitRealtime;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public static class ScheduleSeed
{
    /// <summary>
    /// Linia 5: dus (direcția 0) are 3 curse zilnice (08:00, 08:30, 09:00) prin stațiile 101→102→103, la 5 min între stații;
    /// întors (direcția 1) are o cursă 08:10 prin 103→102→101. Linia 6 are o singură direcție. W1 circulă doar în weekend.
    /// </summary>
    public static void Add(ApplicationDbContext db)
    {
        db.ServiceCalendars.AddRange(
            new ServiceCalendar { ServiceId = "WD", Monday = true, Tuesday = true, Wednesday = true, Thursday = true, Friday = true, StartDate = "2026-01-01", EndDate = "2027-12-31" },
            new ServiceCalendar { ServiceId = "WE", Saturday = true, Sunday = true, StartDate = "2026-01-01", EndDate = "2027-12-31" });

        void Trip(string id, int route, int dir, string service, int startMinutes, params int[] stops)
        {
            db.Trips.Add(new Trip { TripId = id, RouteId = route, DirectionId = dir, ServiceId = service });
            for (var i = 0; i < stops.Length; i++)
            {
                var t = startMinutes + i * 5;
                var time = $"{t / 60:00}:{t % 60:00}:00";
                db.StopTimes.Add(new StopTime { TripId = id, StopId = stops[i], StopSequence = i + 1, ArrivalTime = time, DepartureTime = time });
            }
        }

        Trip("T1", 5, 0, "WD", 8 * 60, 101, 102, 103);
        Trip("T2", 5, 0, "WD", 8 * 60 + 30, 101, 102, 103);
        Trip("T3", 5, 0, "WD", 9 * 60, 101, 102, 103);
        Trip("R1", 5, 1, "WD", 8 * 60 + 10, 103, 102, 101);
        Trip("W1", 5, 0, "WE", 8 * 60 + 20, 101, 102, 103);
        Trip("S1", 6, 0, "WD", 8 * 60 + 15, 201, 202);
        db.SaveChanges();
    }
}

public class TripMatcherTests
{
    // miercuri 2026-11-18, 08:21 ora României (UTC+2)
    private static readonly DateTimeOffset Wednesday0821 = new(2026, 11, 18, 6, 21, 0, TimeSpan.Zero);

    private static (TripMatcher matcher, FakeTimeProvider clock) Create(bool seed = true, int maxDeviation = 1800)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<RouteCalculatorService>();
        var sp = services.BuildServiceProvider();

        if (seed)
        {
            using var scope = sp.CreateScope();
            ScheduleSeed.Add(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }
        var clock = new FakeTimeProvider(Wednesday0821);
        return (new TripMatcher(sp.GetRequiredService<IServiceScopeFactory>(), new TripMatchOptions { MaxDeviationSeconds = maxDeviation },
            NullLogger<TripMatcher>.Instance, clock), clock);
    }

    private static BusPosition Bus(string id, int route = 5, int? direction = 0, int? nextStop = 102) => new()
    {
        Id = id, RouteId = route, DirectionId = direction, NextStationId = nextStop, Latitude = 45.7, Longitude = 24.1, Timestamp = 1
    };

    [Fact]
    public async Task PicksTheScheduledTripClosestInTime_ForTheNextStop()
    {
        var (matcher, clock) = Create();
        // 08:21: în stația 102 T1 pleacă la 08:05 (16 min), T2 la 08:35 (14 min) => T2
        var result = await matcher.AssignAsync(new[] { Bus("route_5_bus_0") }, clock.GetUtcNow(), default);

        var assignment = Assert.Single(result).Value;
        Assert.Equal("T2", assignment.TripId);
        Assert.Equal("08:30:00", assignment.StartTime);
        Assert.Equal("20261118", assignment.StartDate);
    }

    [Fact]
    public async Task TwoVehiclesNeverGetTheSameTrip()
    {
        var (matcher, clock) = Create();
        var result = await matcher.AssignAsync(new[] { Bus("route_5_bus_0"), Bus("route_5_bus_1"), Bus("route_5_bus_2") }, clock.GetUtcNow(), default);

        Assert.Equal("T2", result["route_5_bus_0"].TripId);
        Assert.Equal("T1", result["route_5_bus_1"].TripId);
        // T3 (09:05 în 102) e la 44 min: peste prag, deci al treilea vehicul rămâne fără cursă
        Assert.False(result.ContainsKey("route_5_bus_2"));
    }

    [Fact]
    public async Task AVehicleKeepsItsTripWhileItStaysPlausible_EvenIfAnotherTripIsNowCloser()
    {
        var (matcher, clock) = Create();
        Assert.Equal("T2", (await matcher.AssignAsync(new[] { Bus("route_5_bus_0") }, clock.GetUtcNow(), default))["route_5_bus_0"].TripId);

        // 08:58, lângă stația 103: T2 pleacă la 08:40 (18 min), T3 la 09:10 (12 min) => o potrivire nouă ar da T3
        clock.Advance(TimeSpan.FromMinutes(37));
        var bus = Bus("route_5_bus_0", nextStop: 103);
        var kept = await matcher.AssignAsync(new[] { bus }, clock.GetUtcNow(), default);
        Assert.Equal("T2", kept["route_5_bus_0"].TripId);

        var (fresh, freshClock) = Create();
        freshClock.SetUtcNow(clock.GetUtcNow());
        Assert.Equal("T3", (await fresh.AssignAsync(new[] { bus }, freshClock.GetUtcNow(), default))["route_5_bus_0"].TripId);
    }

    [Fact]
    public async Task AVehicleThatGoesBackwardsOnItsTripIsMatchedAgain()
    {
        var (matcher, clock) = Create();
        // 08:21, următoarea stație 103: T1 pleacă din 103 la 08:10 (11 min), T2 la 08:40 (19 min) => T1
        var first = await matcher.AssignAsync(new[] { Bus("route_5_bus_0", nextStop: 103) }, clock.GetUtcNow(), default);
        Assert.Equal("T1", first["route_5_bus_0"].TripId);

        // următoarea stație e acum 101, înaintea lui 103 pe T1: nu mai înaintează pe cursa lui => potrivire nouă
        // (în 101: T1 08:00 = 21 min, T2 08:30 = 9 min => T2)
        var again = await matcher.AssignAsync(new[] { Bus("route_5_bus_0", nextStop: 101) }, clock.GetUtcNow(), default);
        Assert.Equal("T2", again["route_5_bus_0"].TripId);
    }

    [Fact]
    public async Task AssignmentIsRecomputed_WhenTheVehicleTurnsAround_OrDriftsTooFarFromTheSchedule()
    {
        var (matcher, clock) = Create();
        await matcher.AssignAsync(new[] { Bus("route_5_bus_0") }, clock.GetUtcNow(), default);

        // la capăt întoarce (direcția 1): cursa de întoarcere R1
        var turned = await matcher.AssignAsync(new[] { Bus("route_5_bus_0", direction: 1, nextStop: 102) }, clock.GetUtcNow(), default);
        Assert.Equal("R1", turned["route_5_bus_0"].TripId);

        // peste o oră nicio cursă din această direcție nu mai e în prag
        clock.Advance(TimeSpan.FromHours(1));
        var late = await matcher.AssignAsync(new[] { Bus("route_5_bus_0", direction: 1, nextStop: 102) }, clock.GetUtcNow(), default);
        Assert.Empty(late);
    }

    [Fact]
    public async Task VehiclesWithoutANextStop_OrOnUnknownLines_GetNoTrip()
    {
        var (matcher, clock) = Create();
        var result = await matcher.AssignAsync(new[] { Bus("a", nextStop: null), Bus("b", route: 99), Bus("c", nextStop: 555) }, clock.GetUtcNow(), default);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ASingleDirectionLine_MatchesRegardlessOfTheReportedDirection()
    {
        var (matcher, clock) = Create();
        var result = await matcher.AssignAsync(new[] { Bus("route_6_bus_0", route: 6, direction: 1, nextStop: 202) }, clock.GetUtcNow(), default);
        Assert.Equal("S1", result["route_6_bus_0"].TripId);
    }

    [Fact]
    public async Task OnlyTheTripsOfTheServiceDayAreConsidered()
    {
        var (matcher, clock) = Create();
        // sâmbătă 2026-11-21 08:21: circulă doar W1 (08:20), nu T1-T3
        clock.SetUtcNow(new DateTimeOffset(2026, 11, 21, 6, 21, 0, TimeSpan.Zero));
        var result = await matcher.AssignAsync(new[] { Bus("route_5_bus_0") }, clock.GetUtcNow(), default);
        Assert.Equal("W1", result["route_5_bus_0"].TripId);
        Assert.Equal("20261121", result["route_5_bus_0"].StartDate);
    }

    [Fact]
    public async Task WithoutAScheduleThereAreNoAssignments_AndNoErrors()
    {
        var (matcher, clock) = Create(seed: false);
        Assert.Empty(await matcher.AssignAsync(new[] { Bus("route_5_bus_0") }, clock.GetUtcNow(), default));
    }

    [Fact]
    public async Task TheDeviationLimitIsConfigurable()
    {
        var (strict, clock) = Create(maxDeviation: 10 * 60);       // T2 e la 14 min: în afara pragului
        Assert.Empty(await strict.AssignAsync(new[] { Bus("route_5_bus_0") }, clock.GetUtcNow(), default));
    }

    [Theory]
    [InlineData("08:05:00", 29100)]
    [InlineData("25:10:30", 90630)]
    [InlineData("bad", -1)]
    [InlineData(null, -1)]
    public void GtfsTime_ParsesTimesThatMayExceed24Hours(string? value, int expected) =>
        Assert.Equal(expected, GtfsTime.Parse(value));
}

public class GtfsRealtimeTripFieldsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void AssignedVehiclesAreScheduled_OthersStayUnscheduled()
    {
        var positions = new[]
        {
            new BusPosition { Id = "route_5_bus_0", RouteId = 5, Latitude = 45.7, Longitude = 24.1, Timestamp = Now.ToUnixTimeSeconds(), DirectionId = 0 },
            new BusPosition { Id = "route_5_bus_1", RouteId = 5, Latitude = 45.7, Longitude = 24.1, Timestamp = Now.ToUnixTimeSeconds(), DirectionId = 0 }
        };
        var trips = new Dictionary<string, TripAssignment> { ["route_5_bus_0"] = new("T2", "08:30:00", "20261118") };

        var feed = GtfsRealtimeFeedBuilder.Build(positions, Now, trips: trips);

        var matched = feed.Entity.Single(e => e.Id == "route_5_bus_0").Vehicle.Trip;
        Assert.Equal(("T2", "08:30:00", "20261118"), (matched.TripId, matched.StartTime, matched.StartDate));
        Assert.Equal(TripDescriptor.Types.ScheduleRelationship.Scheduled, matched.ScheduleRelationship);
        Assert.Equal("5", matched.RouteId);

        var other = feed.Entity.Single(e => e.Id == "route_5_bus_1").Vehicle.Trip;
        Assert.False(other.HasTripId);
        Assert.Equal(TripDescriptor.Types.ScheduleRelationship.Unscheduled, other.ScheduleRelationship);
    }
}

public class GtfsRealtimeTripEndpointTests : IClassFixture<GtfsRealtimeTripEndpointTests.ScheduleFactory>
{
    public class ScheduleFactory : AuthWebAppFactory
    {
        // miercuri 08:21 ora României, ca să existe curse „acum”
        public static readonly DateTimeOffset Now = new(2026, 11, 18, 6, 21, 0, TimeSpan.Zero);

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Ingest:ApiKey", "test-key");
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now)));
        }
    }

    private readonly ScheduleFactory _factory;

    public GtfsRealtimeTripEndpointTests(ScheduleFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!db.Trips.Any()) ScheduleSeed.Add(db);
    }

    [Fact]
    public async Task TheFeedCarriesTheTripIdDeducedFromTheSchedule()
    {
        var ingest = _factory.CreateClient();
        ingest.DefaultRequestHeaders.Add("X-Ingest-Key", "test-key");
        await ingest.PostAsJsonAsync("/api/live/positions", new[]
        {
            new { id = "route_5_bus_0", routeId = 5, latitude = 45.79, longitude = 24.15, directionId = 0, nextStationId = 102,
                  timestamp = ScheduleFactory.Now.ToUnixTimeSeconds() - 3 }
        });

        var feed = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/gtfs-rt/vehicle-positions?format=json");
        var trip = feed.GetProperty("entity").EnumerateArray().Single(e => e.GetProperty("id").GetString() == "route_5_bus_0")
            .GetProperty("vehicle").GetProperty("trip");

        Assert.Equal("T2", trip.GetProperty("tripId").GetString());
        Assert.Equal("20261118", trip.GetProperty("startDate").GetString());
        Assert.Equal("SCHEDULED", trip.GetProperty("scheduleRelationship").GetString());
    }
}
