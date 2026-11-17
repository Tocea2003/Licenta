using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Time.Testing;
using TursibBackend.Controllers;
using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class LiveBusStateTests
{
    private static BusPosition Pos(string id = "route_1_bus_0", long ts = 1000, double lat = 45.79, double lon = 24.15) =>
        new() { Id = id, RouteId = 1, Latitude = lat, Longitude = lon, Timestamp = ts };

    [Fact]
    public void Apply_RejectsInvalidPositions()
    {
        var state = new LiveBusState();
        var accepted = state.Apply(new[]
        {
            Pos(id: "legacy_bus"),            // nu începe cu route_
            Pos(lat: 95),                      // latitudine imposibilă
            Pos(lat: 0, lon: 0),               // poziție lipsă
            new BusPosition { Id = "route_1_bus_1", RouteId = 0, Latitude = 45, Longitude = 24, Timestamp = 5 },
            Pos(id: "route_2_bus_0"),          // singura validă
        });

        Assert.Single(accepted);
        Assert.Equal("route_2_bus_0", state.Snapshot().Single().Id);
    }

    [Fact]
    public void Apply_IgnoresOlderPositionForSameBus()
    {
        var state = new LiveBusState();
        state.Apply(new[] { Pos(ts: 2000, lat: 45.80) });
        var accepted = state.Apply(new[] { Pos(ts: 1500, lat: 45.70) });

        Assert.Empty(accepted);
        Assert.Equal(45.80, state.Snapshot().Single().Latitude);
    }

    [Fact]
    public void Apply_FillsMissingTimestampFromServerClock()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(5000));
        var state = new LiveBusState(clock);

        var accepted = state.Apply(new[] { Pos(ts: 0) });

        Assert.Equal(5000, accepted.Single().Timestamp);
    }

    [Fact]
    public void RemoveStale_DropsOnlyBusesThatStoppedReporting()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(10_000));
        var state = new LiveBusState(clock);
        state.Apply(new[] { Pos("route_1_bus_0", ts: 10_000), Pos("route_1_bus_1", ts: 10_000) });

        clock.Advance(TimeSpan.FromMinutes(4));
        state.Apply(new[] { Pos("route_1_bus_1", ts: 10_240) });   // acesta mai raportează
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, state.RemoveStale(TimeSpan.FromMinutes(5)));
        Assert.Equal("route_1_bus_1", state.Snapshot().Single().Id);
    }
}

public class LiveEndpointsTests : IClassFixture<LiveEndpointsTests.LiveFactory>
{
    public class LiveFactory : AuthWebAppFactory
    {
        public string? ApiKey { get; set; } = "test-key";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            if (ApiKey != null) builder.UseSetting("Ingest:ApiKey", ApiKey);
        }
    }

    private readonly LiveFactory _factory;
    public LiveEndpointsTests(LiveFactory factory) => _factory = factory;

    private static object[] Batch(string id, int routeId = 7) =>
        new object[] { new { id, routeId, latitude = 45.79, longitude = 24.15, speed = 30, heading = 90 } };

    private HttpClient ClientWithKey(string? key)
    {
        var client = _factory.CreateClient();
        if (key != null) client.DefaultRequestHeaders.Add(LiveController.ApiKeyHeader, key);
        return client;
    }

    [Fact]
    public async Task Ingest_Returns401_WithoutOrWithWrongKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ClientWithKey(null).PostAsJsonAsync("/api/live/positions", Batch("route_7_bus_0"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ClientWithKey("wrong").PostAsJsonAsync("/api/live/positions", Batch("route_7_bus_0"))).StatusCode);
    }

    [Fact]
    public async Task Ingest_StoresPositions_AndSnapshotReturnsThem()
    {
        var response = await ClientWithKey("test-key").PostAsJsonAsync("/api/live/positions", Batch("route_7_bus_3"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await _factory.CreateClient().GetFromJsonAsync<List<BusPosition>>("/api/live/positions");
        var bus = Assert.Single(snapshot!, b => b.Id == "route_7_bus_3");
        Assert.Equal(7, bus.RouteId);
        Assert.True(bus.Timestamp > 0, "serverul completează timestamp-ul");
    }

    [Fact]
    public async Task Ingest_Returns503_WhenNoKeyConfigured()
    {
        using var factory = new LiveFactory { ApiKey = null };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(LiveController.ApiKeyHeader, "anything");

        var response = await client.PostAsJsonAsync("/api/live/positions", Batch("route_7_bus_0"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task SignalR_ClientGetsSnapshotAndLiveBatches()
    {
        await ClientWithKey("test-key").PostAsJsonAsync("/api/live/positions", Batch("route_8_bus_0", 8));

        var received = new TaskCompletionSource<IReadOnlyList<BusPosition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/buses"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        connection.On<List<BusPosition>>("positions", batch => received.TrySetResult(batch));

        await connection.StartAsync();
        try
        {
            var snapshot = await connection.InvokeAsync<List<BusPosition>>("GetSnapshot");
            Assert.Contains(snapshot, b => b.Id == "route_8_bus_0");

            await ClientWithKey("test-key").PostAsJsonAsync("/api/live/positions", Batch("route_8_bus_1", 8));
            var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("route_8_bus_1", pushed.Single().Id);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
