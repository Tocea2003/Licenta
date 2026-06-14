using Microsoft.AspNetCore.Mvc;
using TursibBackend.Services;
using static TursibBackend.Services.RouteCalculatorService;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutingController : ControllerBase
    {
        private readonly RouteCalculatorService _routeCalculator;
        private readonly ILogger<RoutingController> _logger;

        public RoutingController(RouteCalculatorService routeCalculator, ILogger<RoutingController> logger)
        {
            _routeCalculator = routeCalculator;
            _logger = logger;
        }

        // POST: api/routing/calculate
        [HttpPost("calculate")]
        public async Task<ActionResult<CalculatedRoute>> CalculateRoute([FromBody] RouteRequest request)
        {
            if (request.StartStationId == request.EndStationId)
            {
                return BadRequest(new { message = "Start and end stations must be different" });
            }

            try
            {
                var route = await _routeCalculator.CalculateOptimalRoute(
                    request.StartStationId,
                    request.EndStationId,
                    request.DepartureTime,
                    request.ArrivalTime
                );

                if (route == null)
                {
                    return NotFound(new { message = "No route found between these stations" });
                }

                return Ok(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate route from {Start} to {End}", request.StartStationId, request.EndStationId);
                return StatusCode(500, new { message = "Failed to calculate route. Please try again later." });
            }
        }

        // POST: api/routing/alternatives
        [HttpPost("alternatives")]
        public async Task<ActionResult<List<CalculatedRoute>>> CalculateAlternativeRoutes([FromBody] RouteRequest request)
        {
            if (request.StartStationId == request.EndStationId)
            {
                return BadRequest(new { message = "Start and end stations must be different" });
            }

            try
            {
                var routes = await _routeCalculator.CalculateAlternativeRoutes(
                    request.StartStationId,
                    request.EndStationId,
                    request.DepartureTime,
                    request.ArrivalTime
                );

                return Ok(routes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate alternative routes from {Start} to {End}", request.StartStationId, request.EndStationId);
                return StatusCode(500, new { message = "Failed to calculate alternative routes. Please try again later." });
            }
        }
    }

    public class RouteRequest
    {
        public int StartStationId { get; set; }
        public int EndStationId { get; set; }
        public DateTime? DepartureTime { get; set; }
        public DateTime? ArrivalTime { get; set; }
    }
}
