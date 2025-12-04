using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")] // Doar utilizatorii cu rol Admin pot accesa
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========== ROUTE MANAGEMENT ==========

        // GET: api/admin/routes
        [HttpGet("routes")]
        public async Task<ActionResult<IEnumerable<Models.Route>>> GetAllRoutes()
        {
            return await _context.Routes.ToListAsync();
        }

        // PUT: api/admin/routes/5
        [HttpPut("routes/{id}")]
        public async Task<IActionResult> UpdateRoute(int id, [FromBody] Models.Route route)
        {
            if (id != route.Id)
            {
                return BadRequest("Route ID mismatch");
            }

            var existingRoute = await _context.Routes.FindAsync(id);
            if (existingRoute == null)
            {
                return NotFound();
            }

            existingRoute.RouteNumber = route.RouteNumber;
            existingRoute.Name = route.Name;
            existingRoute.Color = route.Color;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "Error updating route");
            }

            return NoContent();
        }

        // POST: api/admin/routes
        [HttpPost("routes")]
        public async Task<ActionResult<Models.Route>> CreateRoute([FromBody] Models.Route route)
        {
            _context.Routes.Add(route);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetRoute", "Routes", new { id = route.Id }, route);
        }

        // DELETE: api/admin/routes/5
        [HttpDelete("routes/{id}")]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            var route = await _context.Routes.FindAsync(id);
            if (route == null)
            {
                return NotFound();
            }

            _context.Routes.Remove(route);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PATCH: api/admin/routes/5/color
        [HttpPatch("routes/{id}/color")]
        public async Task<IActionResult> UpdateRouteColor(int id, [FromBody] ColorUpdateRequest request)
        {
            var route = await _context.Routes.FindAsync(id);
            if (route == null)
            {
                return NotFound();
            }

            route.Color = request.Color;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ========== STATION MANAGEMENT ==========

        // POST: api/admin/stations
        [HttpPost("stations")]
        public async Task<ActionResult<Station>> CreateStation([FromBody] Station station)
        {
            _context.Stations.Add(station);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetStation", "Stations", new { id = station.Id }, station);
        }

        // PUT: api/admin/stations/5
        [HttpPut("stations/{id}")]
        public async Task<IActionResult> UpdateStation(int id, [FromBody] Station station)
        {
            if (id != station.Id)
            {
                return BadRequest("Station ID mismatch");
            }

            _context.Entry(station).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Stations.Any(s => s.Id == id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // DELETE: api/admin/stations/5
        [HttpDelete("stations/{id}")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var station = await _context.Stations.FindAsync(id);
            if (station == null)
            {
                return NotFound();
            }

            _context.Stations.Remove(station);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ========== ROUTE-STATION MANAGEMENT ==========

        // POST: api/admin/routes/5/stations
        [HttpPost("routes/{routeId}/stations")]
        public async Task<ActionResult> AddStationToRoute(int routeId, [FromBody] RouteStationRequest request)
        {
            var route = await _context.Routes.FindAsync(routeId);
            if (route == null)
            {
                return NotFound("Route not found");
            }

            var station = await _context.Stations.FindAsync(request.StationId);
            if (station == null)
            {
                return NotFound("Station not found");
            }

            var routeStation = new RouteStation
            {
                RouteId = routeId,
                StationId = request.StationId,
                Order = request.Order
            };

            _context.RouteStations.Add(routeStation);
            await _context.SaveChangesAsync();

            return Ok(routeStation);
        }

        // DELETE: api/admin/routes/{routeId}/stations/{stationId}
        [HttpDelete("routes/{routeId}/stations/{stationId}")]
        public async Task<IActionResult> RemoveStationFromRoute(int routeId, int stationId)
        {
            var routeStation = await _context.RouteStations
                .FirstOrDefaultAsync(rs => rs.RouteId == routeId && rs.StationId == stationId);

            if (routeStation == null)
            {
                return NotFound();
            }

            _context.RouteStations.Remove(routeStation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/admin/routes/{routeId}/stations/reorder
        [HttpPut("routes/{routeId}/stations/reorder")]
        public async Task<IActionResult> ReorderStations(int routeId, [FromBody] List<StationOrderRequest> stationOrders)
        {
            var routeStations = await _context.RouteStations
                .Where(rs => rs.RouteId == routeId)
                .ToListAsync();

            foreach (var orderRequest in stationOrders)
            {
                var routeStation = routeStations.FirstOrDefault(rs => rs.StationId == orderRequest.StationId);
                if (routeStation != null)
                {
                    routeStation.Order = orderRequest.Order;
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    // Request models
    public class ColorUpdateRequest
    {
        public string Color { get; set; } = string.Empty;
    }

    public class RouteStationRequest
    {
        public int StationId { get; set; }
        public int Order { get; set; }
    }

    public class StationOrderRequest
    {
        public int StationId { get; set; }
        public int Order { get; set; }
    }
}
