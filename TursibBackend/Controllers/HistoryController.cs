using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;

namespace TursibBackend.Controllers
{
    /// <summary>Recorded live positions, for replaying a time window (admin only).</summary>
    [ApiController]
    [Route("api/history")]
    [Authorize(Roles = "Admin")]
    public class HistoryController : ControllerBase
    {
        public static readonly TimeSpan MaxWindow = TimeSpan.FromHours(1);
        private const int MaxRows = 50_000;

        private readonly ApplicationDbContext _db;

        public HistoryController(ApplicationDbContext db) => _db = db;

        [HttpGet("positions")]
        public async Task<IActionResult> Positions(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? routeId = null,
            [FromQuery] string? vehicleId = null)
        {
            var fromUtc = DateTime.SpecifyKind(from.ToUniversalTime(), DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(to.ToUniversalTime(), DateTimeKind.Utc);
            if (toUtc <= fromUtc)
                return BadRequest(new { message = "'to' must be after 'from'" });
            if (toUtc - fromUtc > MaxWindow)
                return BadRequest(new { message = $"The window can be at most {MaxWindow.TotalMinutes} minutes" });

            var query = _db.VehiclePositionHistory.AsNoTracking()
                .Where(r => r.RecordedAt >= fromUtc && r.RecordedAt < toUtc);
            if (routeId is not null)
                query = query.Where(r => r.RouteId == routeId);
            if (!string.IsNullOrWhiteSpace(vehicleId))
                query = query.Where(r => r.VehicleId == vehicleId);

            var rows = await query
                .OrderBy(r => r.RecordedAt).ThenBy(r => r.VehicleId)
                .Take(MaxRows)
                .Select(r => new
                {
                    r.VehicleId,
                    r.RouteId,
                    r.Latitude,
                    r.Longitude,
                    r.Speed,
                    r.Heading,
                    r.Occupancy,
                    r.RecordedAt
                })
                .ToListAsync();

            return Ok(rows);
        }
    }
}
