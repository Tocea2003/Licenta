using Microsoft.AspNetCore.Mvc;
using TursibBackend.Data;
using TursibBackend.Services;
using Microsoft.EntityFrameworkCore;
using static TursibBackend.Services.RouteCalculatorService;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutingController : ControllerBase
    {
        private readonly RouteCalculatorService _routeCalculator;
        private readonly ApplicationDbContext _context;

        public RoutingController(RouteCalculatorService routeCalculator, ApplicationDbContext context)
        {
            _routeCalculator = routeCalculator;
            _context = context;
        }

        // POST: api/routing/calculate
        [HttpPost("calculate")]
        public async Task<ActionResult<CalculatedRoute>> CalculateRoute([FromBody] RouteRequest request)
        {
            var validationError = await ValidateRouteRequest(request);
            if (validationError != null) return validationError;

            try
            {
                var route = await _routeCalculator.CalculateOptimalRoute(
                    request.StartStationId,
                    request.EndStationId,
                    request.DepartureTime
                );

                if (route == null)
                {
                    return NotFound(new { message = "No route found between these stations" });
                }

                return Ok(route);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to calculate route", error = ex.Message });
            }
        }

        // POST: api/routing/alternatives
        [HttpPost("alternatives")]
        public async Task<ActionResult<List<CalculatedRoute>>> CalculateAlternativeRoutes([FromBody] RouteRequest request)
        {
            var validationError = await ValidateRouteRequest(request);
            if (validationError != null) return validationError;

            try
            {
                var routes = await _routeCalculator.CalculateAlternativeRoutes(
                    request.StartStationId,
                    request.EndStationId,
                    request.DepartureTime
                );

                if (routes.Count == 0)
                {
                    return NotFound(new { message = "No routes found between these stations" });
                }

                return Ok(routes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to calculate alternative routes", error = ex.Message });
            }
        }

        private async Task<ActionResult?> ValidateRouteRequest(RouteRequest request)
        {
            if (request.StartStationId <= 0 || request.EndStationId <= 0)
                return BadRequest(new { message = "Station IDs must be positive integers" });

            if (request.StartStationId == request.EndStationId)
                return BadRequest(new { message = "Start and end stations must be different" });

            var startExists = await _context.Stations.AnyAsync(s => s.Id == request.StartStationId);
            if (!startExists)
                return NotFound(new { message = $"Start station with ID {request.StartStationId} not found" });

            var endExists = await _context.Stations.AnyAsync(s => s.Id == request.EndStationId);
            if (!endExists)
                return NotFound(new { message = $"End station with ID {request.EndStationId} not found" });

            return null;
        }
    }

    public class RouteRequest
    {
        public int StartStationId { get; set; }
        public int EndStationId { get; set; }
        public DateTime? DepartureTime { get; set; }
    }
}
