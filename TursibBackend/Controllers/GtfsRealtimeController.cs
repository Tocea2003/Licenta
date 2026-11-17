using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using TursibBackend.Services;

namespace TursibBackend.Controllers;

/// <summary>Feed-ul standard GTFS-Realtime, consumabil de aplicații terțe (Google Maps, Transit, OneBusAway etc.).</summary>
[Route("api/gtfs-rt")]
[ApiController]
public class GtfsRealtimeController : ControllerBase
{
    private readonly LiveBusState _state;
    private readonly TripMatcher _trips;
    private readonly ILogger<GtfsRealtimeController> _logger;
    private readonly TimeProvider _time;

    public GtfsRealtimeController(LiveBusState state, TripMatcher trips, ILogger<GtfsRealtimeController> logger, TimeProvider? time = null)
    {
        _state = state;
        _trips = trips;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>VehiclePositions în format protobuf; cu <c>?format=json</c> aceeași structură ca JSON, pentru depanare.</summary>
    [HttpGet("vehicle-positions")]
    public async Task<IActionResult> VehiclePositions([FromQuery] string? format = null, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        var positions = _state.Snapshot();

        // trip_id-ul se deduce din orar; dacă nu se poate (fără orar), feed-ul rămâne valid, cu vehicule UNSCHEDULED
        IReadOnlyDictionary<string, TripAssignment>? trips = null;
        try
        {
            var fresh = positions.Where(p => p.Timestamp >= now.ToUnixTimeSeconds() - 120);
            trips = await _trips.AssignAsync(fresh, now, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Potrivirea curselor a eșuat; feed fără trip_id");
        }

        var feed = GtfsRealtimeFeedBuilder.Build(positions, now, trips: trips);
        Response.Headers.CacheControl = "public, max-age=5";

        return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? Content(JsonFormatter.Default.Format(feed), "application/json")
            : File(feed.ToByteArray(), "application/x-protobuf");
    }
}
