using Microsoft.AspNetCore.Mvc;
using TursibBackend.Services;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutingController : ControllerBase
    {
        private readonly RouteCalculatorService _routeCalculator;

        public RoutingController(RouteCalculatorService routeCalculator)
        {
            _routeCalculator = routeCalculator;
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
    }

    public class RouteRequest
    {
        public int StartStationId { get; set; }
        public int EndStationId { get; set; }
        public DateTime? DepartureTime { get; set; }
    }
}
