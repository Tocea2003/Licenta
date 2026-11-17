using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TransitRealtime;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using TursibIngestion;
using TursibMessaging;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TursibBackend.Tests;

// ── Serviciul de ingestie (MQTT -> validare) ────────────────────────────────────────────────────────

public class PositionParserTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    private const string Topic = "tursib/vehicles/route_7_bus_1/position";

    private static (bool ok, VehiclePositionMessage? msg, RejectReason reason) Parse(string topic, string json)
    {
        var ok = PositionParser.TryParse(topic, Encoding.UTF8.GetBytes(json), Now, out var msg, out var reason);
        return (ok, msg, reason);
    }

    [Fact]
    public void Accepts_AValidMessage_AndTakesTheIdFromTheTopic()
    {
        var (ok, msg, _) = Parse(Topic, """{"latitude":45.79,"longitude":24.15,"routeId":7,"speed":30,"timestamp":1799999990}""");
        Assert.True(ok);
        Assert.Equal("route_7_bus_1", msg!.Id);
        Assert.Equal(1799999990, msg.Timestamp);
    }

    [Fact]
    public void FillsInTheTimestamp_WhenMissing()
    {
        var (_, msg, _) = Parse(Topic, """{"latitude":45.79,"longitude":24.15,"routeId":7}""");
        Assert.Equal(Now.ToUnixTimeSeconds(), msg!.Timestamp);
    }

    [Theory]
    [InlineData("tursib/vehicles/route_7_bus_1/position", """{"id":"route_9_bus_0","latitude":45.7,"longitude":24.1,"routeId":7}""", RejectReason.IdMismatch)]
    [InlineData("tursib/other/route_7_bus_1/position", """{"latitude":45.7,"longitude":24.1,"routeId":7}""", RejectReason.InvalidTopic)]
    [InlineData("tursib/vehicles/legacy_bus/position", """{"latitude":45.7,"longitude":24.1,"routeId":7}""", RejectReason.InvalidId)]
    [InlineData(Topic, "not json", RejectReason.InvalidJson)]
    [InlineData(Topic, """{"latitude":45.7,"longitude":24.1,"routeId":0}""", RejectReason.InvalidRoute)]
    [InlineData(Topic, """{"latitude":95,"longitude":24.1,"routeId":7}""", RejectReason.InvalidCoordinates)]
    [InlineData(Topic, """{"latitude":0,"longitude":0,"routeId":7}""", RejectReason.InvalidCoordinates)]
    [InlineData(Topic, """{"latitude":45.7,"longitude":24.1,"routeId":7,"timestamp":1800009999}""", RejectReason.TimestampInFuture)]
    public void Rejects_InvalidMessages_WithTheRightReason(string topic, string json, RejectReason expected)
    {
        var (ok, msg, reason) = Parse(topic, json);
        Assert.False(ok);
        Assert.Null(msg);
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void Rejects_OversizedPayloads()
    {
        var big = new string('x', PositionParser.MaxPayloadBytes + 1);
        var (ok, _, reason) = Parse(Topic, $$"""{"routeId":7,"routeName":"{{big}}"}""");
        Assert.False(ok);
        Assert.Equal(RejectReason.PayloadTooLarge, reason);
    }

    [Fact]
    public void ExtractsTheVehicleIdOnlyFromTheExpectedTopicShape()
    {
        Assert.Equal("route_1_bus_0", PositionParser.VehicleIdFromTopic("tursib/vehicles/route_1_bus_0/position"));
        Assert.Null(PositionParser.VehicleIdFromTopic("tursib/vehicles//position"));
        Assert.Null(PositionParser.VehicleIdFromTopic("tursib/vehicles/a/b/position"));
    }
}

public class PositionIngestorTests
{
    private sealed class FakeSink : IPositionSink
    {
        public List<(string Key, string Json)> Published { get; } = new();
        public List<(string Topic, RejectReason Reason)> Rejected { get; } = new();
        public bool Fail { get; set; }

        public Task PublishAsync(string vehicleId, string json, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("kafka down");
            Published.Add((vehicleId, json));
            return Task.CompletedTask;
        }

        public Task PublishRejectedAsync(string topic, ReadOnlyMemory<byte> payload, RejectReason reason, CancellationToken ct)
        {
            Rejected.Add((topic, reason));
            return Task.CompletedTask;
        }
    }

    private static (PositionIngestor ingestor, FakeSink sink, IngestionMetrics metrics) Create()
    {
        var sink = new FakeSink();
        var metrics = new IngestionMetrics();
        return (new PositionIngestor(sink, metrics, NullLogger<PositionIngestor>.Instance), sink, metrics);
    }

    private static ReadOnlyMemory<byte> Bytes(string json) => Encoding.UTF8.GetBytes(json);
    private const string Valid = """{"latitude":45.79,"longitude":24.15,"routeId":7,"speed":30}""";

    [Fact]
    public async Task ValidMessage_IsPublishedKeyedByVehicle()
    {
        var (ingestor, sink, metrics) = Create();
        await ingestor.HandleAsync("tursib/vehicles/route_7_bus_1/position", Bytes(Valid), default);

        var (key, json) = Assert.Single(sink.Published);
        Assert.Equal("route_7_bus_1", key);
        Assert.Contains("\"id\":\"route_7_bus_1\"", json);
        Assert.Equal(1, metrics.Received);
        Assert.Equal(1, metrics.Published);
    }

    [Fact]
    public async Task InvalidMessage_GoesToTheDeadLetterQueue_AndIsCounted()
    {
        var (ingestor, sink, metrics) = Create();
        await ingestor.HandleAsync("tursib/vehicles/route_7_bus_1/position", Bytes("garbage"), default);

        Assert.Empty(sink.Published);
        Assert.Equal(RejectReason.InvalidJson, Assert.Single(sink.Rejected).Reason);
        Assert.Equal(1, metrics.Rejected[RejectReason.InvalidJson]);
    }

    [Fact]
    public async Task SinkFailure_IsCountedAndDoesNotCrashTheSubscriber()
    {
        var (ingestor, sink, metrics) = Create();
        sink.Fail = true;
        await ingestor.HandleAsync("tursib/vehicles/route_7_bus_1/position", Bytes(Valid), default);

        Assert.Equal(1, metrics.Failed);
        Assert.Equal(0, metrics.Published);
    }

    /// <summary>
    /// Contractul dintre serviciul de ingestie (care scrie pe Kafka) și backend (care citește): tot ce scrie primul
    /// trebuie să se citească corect în al doilea. Păzește de derivă între cele două definiții ale mesajului.
    /// </summary>
    [Fact]
    public async Task WhatIngestionWrites_IsWhatTheBackendReads()
    {
        var (ingestor, sink, _) = Create();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10;
        var payload = $$"""
            {"latitude":45.7981,"longitude":24.1512,"routeId":7,"routeNumber":"7","routeName":"Test","timestamp":{{ts}},
             "speed":31.5,"heading":87.5,"occupancy":42,"status":"approaching","directionId":1,
             "nextStationId":123,"nextStationName":"Piata Mare","nextStationEta":55}
            """;
        await ingestor.HandleAsync("tursib/vehicles/route_7_bus_1/position", Bytes(payload), default);

        Assert.True(KafkaMessageParser.TryParse(sink.Published.Single().Json, out var p));
        Assert.Equal("route_7_bus_1", p!.Id);
        Assert.Equal((45.7981, 24.1512, 7), (p.Latitude, p.Longitude, p.RouteId));
        Assert.Equal(("7", "Test", ts), (p.RouteNumber, p.RouteName, p.Timestamp));
        Assert.Equal((31.5, 87.5, 42, "approaching", 1), (p.Speed, p.Heading, p.Occupancy, p.Status, p.DirectionId));
        Assert.Equal((123, "Piata Mare", 55), (p.NextStationId, p.NextStationName, p.NextStationEta));
    }
}

// ── Consumer Kafka (parsare) și ingestul comun ─────────────────────────────────────────────────────

public class KafkaMessageParserTests
{
    [Fact]
    public void ParsesValidJson_AndRejectsGarbage()
    {
        Assert.True(KafkaMessageParser.TryParse("""{"id":"route_1_bus_0","latitude":45.7,"longitude":24.1,"routeId":1}""", out var p));
        Assert.Equal("route_1_bus_0", p!.Id);
        Assert.False(KafkaMessageParser.TryParse("{nope", out _));
        Assert.False(KafkaMessageParser.TryParse("", out _));
        Assert.False(KafkaMessageParser.TryParse(null, out _));
    }
}

public class LiveIngestServiceTests
{
    [Fact]
    public void AcceptedPositions_UpdateTheStateAndAreRepublishedOnTheBus()
    {
        var state = new LiveBusState();
        var bus = new LiveEventBus();
        var service = new LiveIngestService(state, bus);

        var accepted = service.Ingest(new[]
        {
            new BusPosition { Id = "route_1_bus_0", RouteId = 1, Latitude = 45.7, Longitude = 24.1, Timestamp = 100 },
            new BusPosition { Id = "bad", RouteId = 1, Latitude = 45.7, Longitude = 24.1, Timestamp = 100 }
        });

        Assert.Single(accepted);
        Assert.Single(state.Snapshot());
        Assert.True(bus.Reader.TryRead(out var published));
        Assert.Single(published!);
    }

    [Fact]
    public void ReprocessingTheSameMessage_IsHarmless()
    {
        // livrare „cel puțin o dată” din Kafka: aceeași poziție poate sosi de două ori
        var state = new LiveBusState();
        var service = new LiveIngestService(state, new LiveEventBus());
        var p = new BusPosition { Id = "route_1_bus_0", RouteId = 1, Latitude = 45.7, Longitude = 24.1, Timestamp = 100 };

        Assert.Single(service.Ingest(new[] { p }));
        Assert.Single(service.Ingest(new[] { p }));   // timestamp egal e acceptat, starea rămâne una
        Assert.Single(state.Snapshot());
    }
}

// ── Istoric (replay) ───────────────────────────────────────────────────────────────────────────────

public class PositionHistoryTests
{
    private static BusPosition Pos(string id, long ts, double lat = 45.7) =>
        new() { Id = id, RouteId = 3, Latitude = lat, Longitude = 24.1, Timestamp = ts, Speed = 20, Status = "in_transit", DirectionId = 0 };

    [Fact]
    public void Buffer_KeepsOnePositionPerBusPerInterval()
    {
        var buffer = new PositionHistoryBuffer(new HistoryOptions { SampleSeconds = 15 });
        buffer.Offer(new[] { Pos("a", 1000), Pos("b", 1000) });
        buffer.Offer(new[] { Pos("a", 1005), Pos("a", 1014) });        // prea aproape de cea reținută
        buffer.Offer(new[] { Pos("a", 1015) });                          // la 15 s: se reține

        var records = buffer.Drain();
        Assert.Equal(3, records.Count);
        Assert.Equal(new[] { "a", "b", "a" }, records.Select(r => r.VehicleId));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1000).UtcDateTime, records[0].RecordedAt);
    }

    [Fact]
    public void Buffer_DropsTheOldestWhenFull()
    {
        var buffer = new PositionHistoryBuffer(new HistoryOptions { SampleSeconds = 0, MaxBuffered = 3 });
        buffer.Offer(Enumerable.Range(0, 5).Select(i => Pos($"route_3_bus_{i}", 1000 + i)));

        Assert.Equal(new[] { "route_3_bus_2", "route_3_bus_3", "route_3_bus_4" }, buffer.Drain().Select(r => r.VehicleId));
    }

    private static ServiceProvider Services(string db)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(db));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Writer_FlushesTheBufferToTheDatabase_AndPurgesOldRows()
    {
        using var sp = Services(Guid.NewGuid().ToString());
        var options = new HistoryOptions { Enabled = true, SampleSeconds = 0, RetentionDays = 3 };
        var buffer = new PositionHistoryBuffer(options);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        buffer.Offer(new[] { Pos("route_3_bus_0", now), Pos("route_3_bus_1", now - 5 * 86_400) });   // a doua e veche de 5 zile

        var writer = new PositionHistoryWriter(buffer, options, sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PositionHistoryWriter>.Instance);
        await writer.FlushAsync(default);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // prima rulare face și curățarea: rămâne doar poziția recentă
        var rows = await db.VehiclePositionHistory.ToListAsync();
        Assert.Equal("route_3_bus_0", Assert.Single(rows).VehicleId);
        Assert.Equal(0, buffer.Count);
    }
}

public class PositionHistoryEndpointTests : IClassFixture<PositionHistoryEndpointTests.HistoryFactory>
{
    public class HistoryFactory : AuthWebAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Ingest:ApiKey", "test-key");
            builder.UseSetting("History:Enabled", "true");
            builder.UseSetting("History:SampleSeconds", "0");
            builder.UseSetting("History:FlushSeconds", "1");
        }
    }

    private readonly HistoryFactory _factory;
    public PositionHistoryEndpointTests(HistoryFactory factory) => _factory = factory;

    [Fact]
    public async Task IngestedPositions_AreSavedAndReplayableInOrder()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Key", "test-key");
        var t0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 30;
        foreach (var i in new[] { 0, 1, 2 })
            await client.PostAsJsonAsync("/api/live/positions", new[]
            {
                new { id = "route_4_bus_0", routeId = 4, latitude = 45.7 + i * 0.001, longitude = 24.1, timestamp = t0 + i * 10, directionId = 0 },
                new { id = "route_5_bus_0", routeId = 5, latitude = 45.8, longitude = 24.2, timestamp = t0 + i * 10, directionId = 0 }
            });

        JsonElement rows = default;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            rows = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/live/history?routeId=4");
            if (rows.GetArrayLength() == 3) break;
            await Task.Delay(100);
        }

        Assert.Equal(3, rows.GetArrayLength());
        var latitudes = rows.EnumerateArray().Select(r => r.GetProperty("latitude").GetDouble()).ToList();
        Assert.Equal(latitudes.Order(), latitudes);               // cronologic
        Assert.All(rows.EnumerateArray(), r => Assert.Equal("route_4_bus_0", r.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task HistoryBounds_ReportTheFirstAndLastRecordedMoment_Or204WhenEmpty()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Key", "test-key");
        var t0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60;
        foreach (var i in new[] { 0, 1, 2 })
            await client.PostAsJsonAsync("/api/live/positions", new[]
            {
                new { id = "route_14_bus_0", routeId = 14, latitude = 45.7, longitude = 24.1, timestamp = t0 + i * 10, directionId = 0 }
            });

        JsonElement bounds = default;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var response = await _factory.CreateClient().GetAsync("/api/live/history/bounds?routeId=14");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                bounds = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (bounds.GetProperty("last").GetInt64() == t0 + 20) break;
            }
            await Task.Delay(100);
        }

        Assert.Equal(t0, bounds.GetProperty("first").GetInt64());
        Assert.Equal(t0 + 20, bounds.GetProperty("last").GetInt64());
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.CreateClient().GetAsync("/api/live/history/bounds?routeId=987")).StatusCode);
    }

    [Fact]
    public async Task History_RequiresARouteOrVehicle_AndABoundedWindow()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/live/history")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/live/history?routeId=4&from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/live/history?routeId=4&from=2026-01-02T00:00:00Z&to=2026-01-01T00:00:00Z")).StatusCode);
    }
}

public class HistoryDisabledTests : IClassFixture<AuthWebAppFactory>
{
    private readonly AuthWebAppFactory _factory;
    public HistoryDisabledTests(AuthWebAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/live/history?routeId=1")]
    [InlineData("/api/live/history/bounds?routeId=1")]
    public async Task History_Returns503_WhenNotEnabled(string url) =>
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _factory.CreateClient().GetAsync(url)).StatusCode);
}

// ── GTFS-Realtime ──────────────────────────────────────────────────────────────────────────────────

public class GtfsRealtimeFeedBuilderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static BusPosition Pos(string id, long ts, string? status = "in_transit", int? occupancy = 30) => new()
    {
        Id = id, RouteId = 12, RouteNumber = "12", Latitude = 45.7981, Longitude = 24.1512, Timestamp = ts,
        Speed = 36, Heading = 90, Status = status, Occupancy = occupancy, DirectionId = 1, NextStationId = 345
    };

    [Fact]
    public void BuildsAHeaderAndOneEntityPerFreshVehicle()
    {
        var feed = GtfsRealtimeFeedBuilder.Build(new[]
        {
            Pos("route_12_bus_1", Now.ToUnixTimeSeconds() - 5),
            Pos("route_12_bus_0", Now.ToUnixTimeSeconds() - 10),
            Pos("route_12_bus_9", Now.ToUnixTimeSeconds() - 500)       // prea veche: nu apare
        }, Now);

        Assert.Equal("2.0", feed.Header.GtfsRealtimeVersion);
        Assert.Equal(FeedHeader.Types.Incrementality.FullDataset, feed.Header.Incrementality);
        Assert.Equal((ulong)Now.ToUnixTimeSeconds(), feed.Header.Timestamp);
        Assert.Equal(new[] { "route_12_bus_0", "route_12_bus_1" }, feed.Entity.Select(e => e.Id));
    }

    [Fact]
    public void MapsPositionFieldsToTheStandard()
    {
        var feed = GtfsRealtimeFeedBuilder.Build(new[] { Pos("route_12_bus_0", Now.ToUnixTimeSeconds()) }, Now);
        var v = feed.Entity.Single().Vehicle;

        Assert.Equal("12", v.Trip.RouteId);
        Assert.Equal(1u, v.Trip.DirectionId);
        Assert.Equal(TripDescriptor.Types.ScheduleRelationship.Unscheduled, v.Trip.ScheduleRelationship);
        Assert.Equal("route_12_bus_0", v.Vehicle.Id);
        Assert.Equal(45.7981f, v.Position.Latitude);
        Assert.Equal(24.1512f, v.Position.Longitude);
        Assert.Equal(90f, v.Position.Bearing);
        Assert.Equal(10f, v.Position.Speed, 3);                         // 36 km/h = 10 m/s
        Assert.Equal("345", v.StopId);
        Assert.Equal((ulong)Now.ToUnixTimeSeconds(), v.Timestamp);
    }

    [Theory]
    [InlineData("at_station", VehiclePosition.Types.VehicleStopStatus.StoppedAt)]
    [InlineData("approaching", VehiclePosition.Types.VehicleStopStatus.IncomingAt)]
    [InlineData("in_transit", VehiclePosition.Types.VehicleStopStatus.InTransitTo)]
    [InlineData("departing", VehiclePosition.Types.VehicleStopStatus.InTransitTo)]
    [InlineData(null, VehiclePosition.Types.VehicleStopStatus.InTransitTo)]
    public void MapsStopStatus(string? status, VehiclePosition.Types.VehicleStopStatus expected) =>
        Assert.Equal(expected, GtfsRealtimeFeedBuilder.StopStatus(status));

    [Theory]
    [InlineData(0, VehiclePosition.Types.OccupancyStatus.Empty)]
    [InlineData(25, VehiclePosition.Types.OccupancyStatus.ManySeatsAvailable)]
    [InlineData(50, VehiclePosition.Types.OccupancyStatus.FewSeatsAvailable)]
    [InlineData(70, VehiclePosition.Types.OccupancyStatus.StandingRoomOnly)]
    [InlineData(90, VehiclePosition.Types.OccupancyStatus.CrushedStandingRoomOnly)]
    [InlineData(100, VehiclePosition.Types.OccupancyStatus.Full)]
    public void MapsOccupancy(int percent, VehiclePosition.Types.OccupancyStatus expected) =>
        Assert.Equal(expected, GtfsRealtimeFeedBuilder.Occupancy(percent));

    [Fact]
    public void SurvivesAProtobufRoundTrip()
    {
        var feed = GtfsRealtimeFeedBuilder.Build(new[] { Pos("route_12_bus_0", Now.ToUnixTimeSeconds()) }, Now);
        var parsed = FeedMessage.Parser.ParseFrom(feed.ToByteArray());
        Assert.Equal(feed, parsed);
    }
}

public class GtfsRealtimeEndpointTests : IClassFixture<LiveEndpointsTests.LiveFactory>
{
    private readonly LiveEndpointsTests.LiveFactory _factory;
    public GtfsRealtimeEndpointTests(LiveEndpointsTests.LiveFactory factory) => _factory = factory;

    [Fact]
    public async Task ServesTheLiveFleetAsProtobuf_AndAsJson()
    {
        var ingest = _factory.CreateClient();
        ingest.DefaultRequestHeaders.Add("X-Ingest-Key", "test-key");
        await ingest.PostAsJsonAsync("/api/live/positions", new[]
        {
            new { id = "route_21_bus_0", routeId = 21, routeNumber = "21", latitude = 45.79, longitude = 24.15, speed = 18, heading = 45,
                  status = "at_station", directionId = 0 }
        });

        var response = await _factory.CreateClient().GetAsync("/api/gtfs-rt/vehicle-positions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType!.MediaType);

        var feed = FeedMessage.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync());
        var entity = Assert.Single(feed.Entity, e => e.Id == "route_21_bus_0");
        Assert.Equal(VehiclePosition.Types.VehicleStopStatus.StoppedAt, entity.Vehicle.CurrentStatus);
        Assert.Equal("21", entity.Vehicle.Trip.RouteId);

        var json = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/gtfs-rt/vehicle-positions?format=json");
        Assert.Equal("2.0", json.GetProperty("header").GetProperty("gtfsRealtimeVersion").GetString());
        Assert.Contains(json.GetProperty("entity").EnumerateArray(), e => e.GetProperty("id").GetString() == "route_21_bus_0");
    }
}


// ── Ingestia MQTT integrată în backend (fără broker, doar logica) ──────────────────────────────────

public class EmbeddedMqttIngestTests
{
    private static (EmbeddedMqttIngest ingest, LiveBusState state, LiveEventBus bus) Create()
    {
        var state = new LiveBusState();
        var bus = new LiveEventBus();
        var ingest = new EmbeddedMqttIngest(new MqttOptions(), new LiveIngestService(state, bus),
            NullLogger<EmbeddedMqttIngest>.Instance);
        return (ingest, state, bus);
    }

    private static byte[] Json(string s) => Encoding.UTF8.GetBytes(s);
    private const string Topic = "tursib/vehicles/route_3_bus_1/position";

    [Fact]
    public void ValidMessages_AreBatchedIntoOneEventAndReachTheState()
    {
        var (ingest, state, bus) = Create();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.True(ingest.Handle(Topic, Json($$"""{"latitude":45.7,"longitude":24.1,"routeId":3,"timestamp":{{ts}}}""")));
        Assert.True(ingest.Handle("tursib/vehicles/route_3_bus_2/position", Json($$"""{"latitude":45.8,"longitude":24.1,"routeId":3,"timestamp":{{ts}}}""")));

        Assert.Equal(2, ingest.Flush());
        Assert.Equal(2, state.Snapshot().Count);
        Assert.True(bus.Reader.TryRead(out var batch));
        Assert.Equal(2, batch!.Count);                       // un singur eveniment pentru tot lotul
        Assert.False(bus.Reader.TryRead(out _));
        Assert.Equal(0, ingest.Flush());
    }

    [Fact]
    public void InvalidMessages_AreCountedAndNeverReachTheState()
    {
        var (ingest, state, _) = Create();
        Assert.False(ingest.Handle(Topic, Json("garbage")));
        Assert.False(ingest.Handle(Topic, Json("""{"id":"route_9_bus_9","latitude":45.7,"longitude":24.1,"routeId":3}""")));   // id fals
        Assert.False(ingest.Handle("other/topic", Json("{}")));

        ingest.Flush();
        Assert.Empty(state.Snapshot());
        Assert.Equal(3, ingest.Received);
        Assert.Equal(3, ingest.Rejected);
    }

    [Fact]
    public void MapsEveryFieldOfTheMessageToABusPosition()
    {
        var message = new VehiclePositionMessage
        {
            Id = "route_1_bus_0", Latitude = 1, Longitude = 2, RouteId = 3, RouteNumber = "n", RouteName = "r", Timestamp = 4, Speed = 5,
            Heading = 6, Occupancy = 7, Status = "s", DirectionId = 1, NextStationId = 8, NextStationName = "ns", NextStationEta = 9
        };
        var p = EmbeddedMqttIngest.ToBusPosition(message);
        Assert.Equal((message.Id, 1d, 2d, 3, "n", "r", 4L, 5d, 6d, 7, "s", 1, 8, "ns", 9),
            (p.Id, p.Latitude, p.Longitude, p.RouteId, p.RouteNumber, p.RouteName, p.Timestamp, p.Speed, p.Heading, p.Occupancy,
             p.Status, p.DirectionId, p.NextStationId, p.NextStationName, p.NextStationEta));
    }
}

public class BrokerSecurityOptionsTests
{
    [Fact]
    public void MqttOptions_BuildTlsAndCredentials_AndSharedSubscriptions()
    {
        var plain = new MqttOptions { Host = "h", Port = 1883 }.Build("t");
        Assert.False(plain.ChannelOptions.TlsOptions.UseTls);

        var secure = new MqttOptions { Host = "broker.example", Port = 8883, UseTls = true, Username = "u", Password = "p" }.Build("t");
        Assert.True(secure.ChannelOptions.TlsOptions.UseTls);
        Assert.Equal("u", secure.Credentials.GetUserName(secure));

        Assert.Equal("tursib/vehicles/+/position", new MqttOptions().SubscriptionFilter);
        Assert.Equal("$share/ingest/tursib/vehicles/+/position", new MqttOptions { SharedGroup = "ingest" }.SubscriptionFilter);
    }

    [Fact]
    public void KafkaConnectionOptions_ApplyTheSecuritySettingsToAClientConfig()
    {
        var options = new KafkaConnectionOptions
        {
            BootstrapServers = "broker:9092", SecurityProtocol = SecurityProtocol.SaslSsl, SaslMechanism = SaslMechanism.ScramSha256,
            SaslUsername = "u", SaslPassword = "p", SslCaLocation = "/ca.pem"
        };
        var config = new ConsumerConfig();
        options.Apply(config);

        Assert.Equal("broker:9092", config.BootstrapServers);
        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha256, config.SaslMechanism);
        Assert.Equal(("u", "p", "/ca.pem"), (config.SaslUsername, config.SaslPassword, config.SslCaLocation));

        var local = new ConsumerConfig();
        new KafkaConnectionOptions { BootstrapServers = "localhost:9092" }.Apply(local);
        Assert.Null(local.SecurityProtocol);                // local: fără securitate
    }

    [Fact]
    public void KafkaOptions_BindFromEnvironmentStyleConfiguration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = "x.upstash.io:9092", ["Kafka:SecurityProtocol"] = "SaslSsl", ["Kafka:SaslMechanism"] = "ScramSha256",
            ["Kafka:SaslUsername"] = "user", ["Kafka:SaslPassword"] = "pw", ["Kafka:Topic"] = "positions", ["Kafka:GroupId"] = "g"
        }).Build();

        var options = config.GetSection("Kafka").Get<TursibBackend.Services.KafkaOptions>()!;
        Assert.Equal(SecurityProtocol.SaslSsl, options.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha256, options.SaslMechanism);
        Assert.Equal(("positions", "g"), (options.Topic, options.GroupId));
    }
}
