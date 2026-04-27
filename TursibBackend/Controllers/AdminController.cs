using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using System.Text.Json;

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
            return await _context.Routes.AsNoTracking().ToListAsync();
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
                    return NotFound();
                return StatusCode(500, "Conflict writing station. Please refresh and try again.");
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

        // ========== STATISTICS ==========

        // GET: api/admin/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<StatisticsResponse>> GetStatistics()
        {
            try
            {
                // Run all count queries concurrently
                var routesTask   = _context.Routes.CountAsync();
                var stationsTask = _context.Stations.CountAsync();
                var busesTask    = _context.Buses.CountAsync();
                await Task.WhenAll(routesTask, stationsTask, busesTask);

                var totalRoutes   = routesTask.Result;
                var totalStations = stationsTask.Result;
                var totalBuses    = busesTask.Result;
                var activeBuses   = busesTask.Result;

                // Calculate average occupancy (simulated)
                var avgOccupancy = 0;
                if (activeBuses > 0)
                {
                    // In real scenario, fetch from Firebase
                    avgOccupancy = 45; // Mock data
                }

                // Get route statistics
                var routeStats = await _context.Routes
                    .Select(r => new RouteStatistic
                    {
                        RouteId = r.Id,
                        RouteNumber = r.RouteNumber,
                        Name = r.Name,
                        ActiveBuses = 0, // Would be calculated from Firebase
                        AverageOccupancy = 0 // Would be calculated from Firebase
                    })
                    .ToListAsync();

                // Get station statistics (mock data for top stations)
                var stationStats = new List<StationStatistic>
                {
                    new StationStatistic { StationName = "Piața Mare", PassengerCount = 1456 },
                    new StationStatistic { StationName = "Gara CFR", PassengerCount = 1342 },
                    new StationStatistic { StationName = "Autogării", PassengerCount = 1128 },
                    new StationStatistic { StationName = "Strand", PassengerCount = 998 },
                    new StationStatistic { StationName = "Kaufland", PassengerCount = 887 }
                };

                var response = new StatisticsResponse
                {
                    TotalRoutes = totalRoutes,
                    TotalStations = totalStations,
                    TotalBuses = totalBuses,
                    ActiveBuses = activeBuses,
                    AverageOccupancy = avgOccupancy,
                    RouteStatistics = routeStats,
                    TopStations = stationStats,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve statistics", error = ex.Message });
            }
        }

        // ========== USER MANAGEMENT ==========

        // GET: api/admin/users
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Role,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // PATCH: api/admin/users/5/role
        [HttpPatch("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateUserRoleRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Validate role
            if (request.Role != "Admin" && request.Role != "User")
            {
                return BadRequest(new { message = "Invalid role. Must be 'Admin' or 'User'" });
            }

            user.Role = request.Role;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User role updated to {request.Role}", user = new { user.Id, user.Username, user.Role } });
        }

        // DELETE: api/admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            if (user.Role == "Admin")
            {
                var adminCount = await _context.Users.CountAsync(u => u.Role == "Admin");
                if (adminCount <= 1)
                    return BadRequest(new { message = "Nu poți șterge ultimul administrator." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ========== BUS MANAGEMENT ==========

        // GET: api/admin/buses
        [HttpGet("buses")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllBuses()
        {
            var buses = await _context.Buses
                .AsNoTracking()
                .Include(b => b.CurrentRoute)
                .Select(b => new
                {
                    b.Id,
                    b.LicensePlate,
                    b.InternalName,
                    b.CurrentRouteId,
                    currentRouteName   = b.CurrentRoute != null ? b.CurrentRoute.Name : null,
                    currentRouteNumber = b.CurrentRoute != null ? b.CurrentRoute.RouteNumber : null
                })
                .ToListAsync();
            return Ok(buses);
        }

        // POST: api/admin/buses
        [HttpPost("buses")]
        public async Task<IActionResult> CreateBus([FromBody] TursibBackend.Models.Bus bus)
        {
            _context.Buses.Add(bus);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAllBuses), new { id = bus.Id }, new { bus.Id, bus.LicensePlate, bus.InternalName, bus.CurrentRouteId });
        }

        // PUT: api/admin/buses/{id}
        [HttpPut("buses/{id}")]
        public async Task<IActionResult> UpdateBus(int id, [FromBody] TursibBackend.Models.Bus bus)
        {
            if (id != bus.Id) return BadRequest("Bus ID mismatch");
            var existing = await _context.Buses.FindAsync(id);
            if (existing == null) return NotFound();

            existing.LicensePlate  = bus.LicensePlate;
            existing.InternalName  = bus.InternalName;
            existing.CurrentRouteId = bus.CurrentRouteId;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/admin/buses/{id}
        [HttpDelete("buses/{id}")]
        public async Task<IActionResult> DeleteBus(int id)
        {
            var bus = await _context.Buses.FindAsync(id);
            if (bus == null) return NotFound();
            _context.Buses.Remove(bus);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ========== TICKET MANAGEMENT ==========

        // GET: api/admin/tickets
        [HttpGet("tickets")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllTickets()
        {
            var tickets = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.User)
                .Include(t => t.Payment)
                .OrderByDescending(t => t.PurchasedAt)
                .Select(t => new
                {
                    t.Id,
                    t.TicketType,
                    t.PriceRon,
                    t.Status,
                    t.PurchasedAt,
                    t.ValidFrom,
                    t.ValidUntil,
                    t.QrToken,
                    username = t.User != null ? t.User.Username : null,
                    userId   = t.UserId,
                    payment  = t.Payment != null ? new
                    {
                        t.Payment.Id,
                        t.Payment.Amount,
                        t.Payment.CardLast4,
                        t.Payment.CardBrand,
                        t.Payment.Status
                    } : null
                })
                .ToListAsync();
            return Ok(tickets);
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

    // Statistics response models
    public class StatisticsResponse
    {
        public int TotalRoutes { get; set; }
        public int TotalStations { get; set; }
        public int TotalBuses { get; set; }
        public int ActiveBuses { get; set; }
        public int AverageOccupancy { get; set; }
        public List<RouteStatistic> RouteStatistics { get; set; } = new();
        public List<StationStatistic> TopStations { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class RouteStatistic
    {
        public int RouteId { get; set; }
        public string RouteNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ActiveBuses { get; set; }
        public int AverageOccupancy { get; set; }
    }

    public class StationStatistic
    {
        public string StationName { get; set; } = string.Empty;
        public int PassengerCount { get; set; }
    }

    // Request model for updating user role
    public class UpdateUserRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}
