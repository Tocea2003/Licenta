using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransitRealtime;
using TursibBackend.Hubs;
using TursibBackend.Services.Events;
using Xunit;
using ContractPosition = Tursib.Contracts.VehiclePosition;
using RtVehiclePosition = TransitRealtime.VehiclePosition;

namespace TursibBackend.Tests;

public class LiveVehicleStateTests
{
    private static ContractPosition At(string id, long ts) => new()
    {
        VehicleId = id, RouteId = 1, Latitude = 45.79, Longitude = 24.15, Timestamp = ts
    };

    [Fact]
    public void Upsert_KeepsTheNewestPositionPerVehicle()
    {
        var state = new LiveVehicleState();

        Assert.True(state.Upsert(At("a", 100)));
        Assert.True(state.Upsert(At("a", 105)));
        Assert.False(state.Upsert(At("a", 101)));   // late redelivery is ignored

        var only = Assert.Single(state.Snapshot());
        Assert.Equal(105, only.Timestamp);
    }

    [Fact]
    public void DrainChanged_ReturnsEachChangedVehicleOnce()
    {
        var state = new LiveVehicleState();
        state.Upsert(At("a", 100));
        state.Upsert(At("a", 101));
        state.Upsert(At("b", 100));

        var changed = state.DrainChanged();
        Assert.Equal(2, changed.Count);
        Assert.Contains(changed, p => p.VehicleId == "a" && p.Timestamp == 101);
        Assert.Empty(state.DrainChanged());
    }

    [Fact]
    public void Prune_RemovesVehiclesThatStoppedReporting()
    {
        var state = new LiveVehicleState();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000);
        state.Upsert(At("old", 800));
        state.Upsert(At("fresh", 990));

        var removed = state.Prune(now, TimeSpan.FromMinutes(2));

        Assert.Equal(["old"], removed);
        Assert.Equal("fresh", Assert.Single(state.Snapshot()).VehicleId);
    }
}

public class HistorySamplerTests
{
    [Fact]
    public void RecordsAtMostOncePerIntervalPerVehicle()
    {
        var sampler = new HistorySampler(TimeSpan.FromSeconds(5));
        ContractPosition P(string id, long ts) => new() { VehicleId = id, RouteId = 1, Timestamp = ts };

        Assert.True(sampler.ShouldRecord(P("a", 100)));
        Assert.False(sampler.ShouldRecord(P("a", 102)));
        Assert.False(sampler.ShouldRecord(P("a", 104)));
        Assert.True(sampler.ShouldRecord(P("a", 105)));
        Assert.True(sampler.ShouldRecord(P("b", 101)));   // independent per vehicle
    }
}

public class GtfsRealtimeFeedBuilderTests
{
    private static readonly ContractPosition Sample = new()
    {
        VehicleId = "route_12_bus_0",
        RouteId = 12,
        RouteNumber = "12",
        Latitude = 45.7983,
        Longitude = 24.1256,
        Timestamp = 1_800_000_000,
        Speed = 36,
        Heading = 90,
        Occupancy = 40,
        Status = "at_station",
        DirectionId = 1,
        NextStationId = 3110
    };

    [Fact]
    public void MapsAPositionToAVehicleEntity()
    {
        var entity = GtfsRealtimeFeedBuilder.ToEntity(Sample);
        var v = entity.Vehicle;

        Assert.Equal("route_12_bus_0", entity.Id);
        Assert.Equal("route_12_bus_0", v.Vehicle.Id);
        Assert.Equal("12", v.Vehicle.Label);
        Assert.Equal("12", v.Trip.RouteId);
        Assert.Equal(1u, v.Trip.DirectionId);
        Assert.Equal(45.7983f, v.Position.Latitude);
        Assert.Equal(10f, v.Position.Speed, 3);           // 36 km/h = 10 m/s
        Assert.Equal(1_800_000_000ul, v.Timestamp);
        Assert.Equal(RtVehiclePosition.Types.VehicleStopStatus.StoppedAt, v.CurrentStatus);
        Assert.Equal("3110", v.StopId);
        Assert.Equal(40u, v.OccupancyPercentage);
    }

    [Theory]
    [InlineData("approaching", RtVehiclePosition.Types.VehicleStopStatus.IncomingAt)]
    [InlineData("in_transit", RtVehiclePosition.Types.VehicleStopStatus.InTransitTo)]
    [InlineData(null, RtVehiclePosition.Types.VehicleStopStatus.InTransitTo)]
    public void MapsStatusToStopStatus(string? status, RtVehiclePosition.Types.VehicleStopStatus expected)
    {
        Assert.Equal(expected, GtfsRealtimeFeedBuilder.ToEntity(Sample with { Status = status }).Vehicle.CurrentStatus);
    }

    [Fact]
    public void FeedIsAFullDatasetThatRoundTripsThroughProtobuf()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_005);
        var feed = GtfsRealtimeFeedBuilder.Build([Sample, Sample with { VehicleId = "route_12_bus_1" }], now);

        var parsed = FeedMessage.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(feed));
        Assert.Equal("2.0", parsed.Header.GtfsRealtimeVersion);
        Assert.Equal(FeedHeader.Types.Incrementality.FullDataset, parsed.Header.Incrementality);
        Assert.Equal(1_800_000_005ul, parsed.Header.Timestamp);
        Assert.Equal(2, parsed.Entity.Count);
    }
}

/// <summary>App with the event pipeline switched on but without the Kafka consumer or DB writer.</summary>
public class EventPipelineWebAppFactory : AuthWebAppFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Kafka:BootstrapServers", "localhost:1");
        builder.ConfigureServices(services =>
        {
            // No broker in tests: keep the hub and broadcaster, drop Kafka and the history writer
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                (d.ImplementationType == typeof(VehiclePositionConsumer) || d.ImplementationType == typeof(HistoryWriter)))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);
        });
    }
}

public class VehiclesHubTests : IClassFixture<EventPipelineWebAppFactory>
{
    private readonly EventPipelineWebAppFactory _factory;

    public VehiclesHubTests(EventPipelineWebAppFactory factory) => _factory = factory;

    private HubConnection Connect() => new HubConnectionBuilder()
        .WithUrl(new Uri(_factory.Server.BaseAddress, VehiclesHub.Path), o =>
        {
            o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            o.Transports = HttpTransportType.LongPolling;
        })
        .Build();

    [Fact]
    public async Task NewClient_ReceivesSnapshot_ThenBatchedUpdates()
    {
        var state = _factory.Services.GetRequiredService<LiveVehicleState>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        state.Upsert(new ContractPosition { VehicleId = "route_1_bus_0", RouteId = 1, Latitude = 45.79, Longitude = 24.15, Timestamp = now });
        state.DrainChanged();

        var snapshot = new TaskCompletionSource<List<ContractPosition>>();
        var update = new TaskCompletionSource<List<ContractPosition>>();
        await using var connection = Connect();
        connection.On<List<ContractPosition>>(VehiclesHub.SnapshotMessage, s => snapshot.TrySetResult(s));
        connection.On<List<ContractPosition>>(VehiclesHub.PositionsMessage, p => update.TrySetResult(p));

        await connection.StartAsync();
        var received = await snapshot.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(received, p => p.VehicleId == "route_1_bus_0");

        state.Upsert(new ContractPosition { VehicleId = "route_1_bus_0", RouteId = 1, Latitude = 45.80, Longitude = 24.15, Timestamp = now + 1 });
        var changed = await update.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(45.80, Assert.Single(changed).Latitude);
    }

    [Fact]
    public async Task GtfsRealtimeEndpoint_ServesProtobufAndJson()
    {
        var state = _factory.Services.GetRequiredService<LiveVehicleState>();
        state.Upsert(new ContractPosition { VehicleId = "route_9_bus_2", RouteId = 9, Latitude = 45.79, Longitude = 24.15, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        var client = _factory.CreateClient();

        var pb = await client.GetAsync("/gtfs-rt/vehicle-positions");
        Assert.Equal("application/x-protobuf", pb.Content.Headers.ContentType?.MediaType);
        var feed = FeedMessage.Parser.ParseFrom(await pb.Content.ReadAsByteArrayAsync());
        Assert.Contains(feed.Entity, e => e.Id == "route_9_bus_2");

        var json = await client.GetStringAsync("/gtfs-rt/vehicle-positions?format=json");
        Assert.Contains("route_9_bus_2", json);
    }
}

public class EventPipelineDisabledTests : IClassFixture<AuthWebAppFactory>
{
    private readonly AuthWebAppFactory _factory;

    public EventPipelineDisabledTests(AuthWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task WithoutKafka_HubIsNotMapped_AndFeedIsEmpty()
    {
        var client = _factory.CreateClient();

        // The frontend relies on this 404 to fall back to Firebase
        var negotiate = await client.PostAsync($"{VehiclesHub.Path}/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.NotFound, negotiate.StatusCode);

        var pb = await client.GetAsync("/gtfs-rt/vehicle-positions");
        pb.EnsureSuccessStatusCode();
        Assert.Empty(FeedMessage.Parser.ParseFrom(await pb.Content.ReadAsByteArrayAsync()).Entity);
    }
}

public class HistoryEndpointTests : IClassFixture<AuthWebAppFactory>
{
    private readonly AuthWebAppFactory _factory;

    public HistoryEndpointTests(AuthWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task RequiresAuthentication()
    {
        var response = await _factory.CreateClient().GetAsync(
            "/api/history/positions?from=2026-11-05T08:00:00Z&to=2026-11-05T08:30:00Z");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
