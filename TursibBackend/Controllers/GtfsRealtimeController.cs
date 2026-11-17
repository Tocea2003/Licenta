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
    private readonly TimeProvider _time;

    public GtfsRealtimeController(LiveBusState state, TimeProvider? time = null)
    {
        _state = state;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>VehiclePositions în format protobuf; cu <c>?format=json</c> aceeași structură ca JSON, pentru depanare.</summary>
    [HttpGet("vehicle-positions")]
    public IActionResult VehiclePositions([FromQuery] string? format = null)
    {
        var feed = GtfsRealtimeFeedBuilder.Build(_state.Snapshot(), _time.GetUtcNow());
        Response.Headers.CacheControl = "public, max-age=5";

        return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? Content(JsonFormatter.Default.Format(feed), "application/json")
            : File(feed.ToByteArray(), "application/x-protobuf");
    }
}
