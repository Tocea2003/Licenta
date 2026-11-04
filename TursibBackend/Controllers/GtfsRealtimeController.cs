using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using TursibBackend.Services.Events;

namespace TursibBackend.Controllers
{
    /// <summary>
    /// Standard GTFS-Realtime feed, consumable by any GTFS-RT client
    /// (Google Maps-style apps, OneBusAway, validators).
    /// </summary>
    [ApiController]
    [Route("gtfs-rt")]
    public class GtfsRealtimeController : ControllerBase
    {
        private readonly LiveVehicleState _state;

        public GtfsRealtimeController(LiveVehicleState state) => _state = state;

        /// <summary>Binary protobuf; add ?format=json for a readable version.</summary>
        [HttpGet("vehicle-positions")]
        [HttpGet("vehicle-positions.pb")]
        public IActionResult VehiclePositions([FromQuery] string? format = null)
        {
            var feed = GtfsRealtimeFeedBuilder.Build(_state.Snapshot(), DateTimeOffset.UtcNow);

            Response.Headers.CacheControl = "no-cache";
            return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
                ? Content(JsonFormatter.Default.Format(feed), "application/json")
                : File(feed.ToByteArray(), "application/x-protobuf");
        }
    }
}
