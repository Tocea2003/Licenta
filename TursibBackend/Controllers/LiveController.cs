using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Services;

namespace TursibBackend.Controllers;

[Route("api/live")]
[ApiController]
public class LiveController : ControllerBase
{
    public const string ApiKeyHeader = "X-Ingest-Key";

    private readonly LiveBusState _state;
    private readonly LiveIngestService _ingest;
    private readonly IConfiguration _config;
    private readonly BunchingDetector _bunching;
    private readonly ApplicationDbContext _db;
    private readonly HistoryOptions _history;

    public LiveController(LiveBusState state, LiveIngestService ingest, IConfiguration config, BunchingDetector bunching,
        ApplicationDbContext db, HistoryOptions history)
    {
        _state = state;
        _ingest = ingest;
        _config = config;
        _bunching = bunching;
        _db = db;
        _history = history;
    }

    /// <summary>Alertele de bunching: cele active acum și cele încheiate recent.</summary>
    [HttpGet("bunching")]
    public IActionResult GetBunching() => Ok(new { active = _bunching.Active, recent = _bunching.Recent });

    /// <summary>Ultima poziție cunoscută a tuturor autobuzelor (același conținut ca snapshot-ul SignalR).</summary>
    [HttpGet("positions")]
    public ActionResult<IReadOnlyList<BusPosition>> GetPositions() => Ok(_state.Snapshot());

    /// <summary>Ingest: simulatorul (sau un vehicul real) trimite un lot de poziții. Necesită cheia de ingest.</summary>
    [HttpPost("positions")]
    public IActionResult Ingest([FromBody] List<BusPosition> batch)
    {
        var expected = Environment.GetEnvironmentVariable("INGEST_API_KEY") ?? _config["Ingest:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Ingest nu este configurat (INGEST_API_KEY)." });

        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var provided) || !KeysEqual(provided.ToString(), expected))
            return Unauthorized(new { message = "Cheie de ingest invalidă." });

        if (batch.Count > LiveBusState.MaxBatchSize)
            return BadRequest(new { message = $"Maximum {LiveBusState.MaxBatchSize} poziții per cerere." });

        var accepted = _ingest.Ingest(batch);
        return Ok(new { received = batch.Count, accepted = accepted.Count });
    }

    /// <summary>
    /// Istoricul pozițiilor pentru replay, în ordine cronologică. Necesită <c>History:Enabled</c>.
    /// Interval implicit: ultimele 10 minute; maximum 6 ore și 20 000 de poziții per cerere.
    /// </summary>
    [HttpGet("history")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int? routeId, [FromQuery] string? vehicleId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int limit = 5000, CancellationToken ct = default)
    {
        if (!_history.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Istoricul nu este activat (History:Enabled)." });
        if (routeId == null && string.IsNullOrWhiteSpace(vehicleId))
            return BadRequest(new { message = "Specifică routeId sau vehicleId." });

        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddMinutes(-10)).ToUniversalTime();
        if (start >= end) return BadRequest(new { message = "from trebuie să fie înaintea lui to." });
        if (end - start > TimeSpan.FromHours(6)) return BadRequest(new { message = "Interval maxim: 6 ore." });

        var query = _db.VehiclePositionHistory.AsNoTracking().Where(r => r.RecordedAt >= start && r.RecordedAt <= end);
        if (routeId != null) query = query.Where(r => r.RouteId == routeId);
        if (!string.IsNullOrWhiteSpace(vehicleId)) query = query.Where(r => r.VehicleId == vehicleId);

        var rows = await query.OrderBy(r => r.RecordedAt).ThenBy(r => r.VehicleId)
            .Take(Math.Clamp(limit, 1, 20_000)).ToListAsync(ct);
        return Ok(rows.Select(r => new
        {
            id = r.VehicleId, routeId = r.RouteId, latitude = r.Latitude, longitude = r.Longitude,
            speed = r.Speed, heading = r.Heading, occupancy = r.Occupancy, status = r.Status, directionId = r.DirectionId,
            timestamp = new DateTimeOffset(DateTime.SpecifyKind(r.RecordedAt, DateTimeKind.Utc)).ToUnixTimeSeconds()
        }));
    }

    /// <summary>Intervalul pentru care există istoric (ca interfața de replay să propună o fereastră cu date).</summary>
    [HttpGet("history/bounds")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> GetHistoryBounds([FromQuery] int? routeId, CancellationToken ct = default)
    {
        if (!_history.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Istoricul nu este activat (History:Enabled)." });

        var query = _db.VehiclePositionHistory.AsNoTracking().AsQueryable();
        if (routeId != null) query = query.Where(r => r.RouteId == routeId);

        var first = await query.OrderBy(r => r.RecordedAt).Select(r => (DateTime?)r.RecordedAt).FirstOrDefaultAsync(ct);
        if (first == null) return NoContent();
        var last = await query.OrderByDescending(r => r.RecordedAt).Select(r => r.RecordedAt).FirstAsync(ct);

        long Unix(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return Ok(new { first = Unix(first.Value), last = Unix(last) });
    }

    private static bool KeysEqual(string a, string b)
    {
        var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }
}
