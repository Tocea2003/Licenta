using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;

namespace TursibBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShapesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ShapesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/shapes/{shapeId}
        [HttpGet("{shapeId}")]
        public async Task<ActionResult<IEnumerable<ShapePointDto>>> GetShapePoints(string shapeId)
        {
            var shapePoints = await _context.Database
                .SqlQueryRaw<ShapePointDto>(@"
                    SELECT Latitude, Longitude, Sequence 
                    FROM Shapes 
                    WHERE ShapeId = {0}
                    ORDER BY Sequence", shapeId)
                .ToListAsync();

            if (!shapePoints.Any())
            {
                return NotFound();
            }

            return Ok(shapePoints);
        }

        // GET: api/shapes/route/{routeId}
        [HttpGet("route/{routeId}")]
        public async Task<ActionResult<object>> GetRouteShape(int routeId)
        {
            // Găsește un trip reprezentativ pentru traseu (prima cursă)
            var trip = await _context.Database
                .SqlQueryRaw<TripDto>(@"
                    SELECT TripId, ShapeId, DirectionId 
                    FROM Trips 
                    WHERE RouteId = {0}
                    LIMIT 1", routeId)
                .FirstOrDefaultAsync();

            if (trip == null || string.IsNullOrEmpty(trip.ShapeId))
            {
                return NotFound();
            }

            var shapePoints = await _context.Database
                .SqlQueryRaw<ShapePointDto>(@"
                    SELECT Latitude, Longitude, Sequence 
                    FROM Shapes 
                    WHERE ShapeId = {0}
                    ORDER BY Sequence", trip.ShapeId)
                .ToListAsync();

            return Ok(new
            {
                routeId,
                shapeId = trip.ShapeId,
                directionId = trip.DirectionId,
                points = shapePoints
            });
        }

        // GET: api/shapes/route/{routeId}/segment?fromStationId={fromId}&toStationId={toId}
        [HttpGet("route/{routeId}/segment")]
        public async Task<ActionResult<object>> GetRouteSegment(
            int routeId, 
            [FromQuery] int fromStationId, 
            [FromQuery] int toStationId)
        {
            // Găsește trip-ul pentru traseu
            var trip = await _context.Database
                .SqlQueryRaw<TripDto>(@"
                    SELECT TripId, ShapeId, DirectionId 
                    FROM Trips 
                    WHERE RouteId = {0}
                    LIMIT 1", routeId)
                .FirstOrDefaultAsync();

            if (trip == null || string.IsNullOrEmpty(trip.ShapeId))
            {
                return NotFound("Trip not found for route");
            }

            // Găsește secvența stațiilor în trip
            var stopSequences = await _context.Database
                .SqlQueryRaw<StopSequenceDto>(@"
                    SELECT StopId, StopSequence 
                    FROM StopTimes 
                    WHERE TripId = {0} AND (StopId = {1} OR StopId = {2})
                    ORDER BY StopSequence", trip.TripId, fromStationId, toStationId)
                .ToListAsync();

            if (stopSequences.Count != 2)
            {
                return NotFound("Stations not found in trip");
            }

            var fromSeq = stopSequences.First(s => s.StopId == fromStationId).StopSequence;
            var toSeq = stopSequences.First(s => s.StopId == toStationId).StopSequence;

            // Asigură-te că from vine înainte de to
            if (fromSeq > toSeq)
            {
                (fromSeq, toSeq) = (toSeq, fromSeq);
            }

            // Obține toate punctele shape pentru întregul traseu
            var allShapePoints = await _context.Database
                .SqlQueryRaw<ShapePointDto>(@"
                    SELECT Latitude, Longitude, Sequence 
                    FROM Shapes 
                    WHERE ShapeId = {0}
                    ORDER BY Sequence", trip.ShapeId)
                .ToListAsync();

            // Pentru simplitate, returnează toate punctele
            // În viitor, poți calcula exact care puncte shape corespund segmentului dintre stații
            return Ok(new
            {
                routeId,
                shapeId = trip.ShapeId,
                fromStationId,
                toStationId,
                points = allShapePoints
            });
        }
    }

    public class ShapePointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Sequence { get; set; }
    }

    public class TripDto
    {
        public string TripId { get; set; } = "";
        public string? ShapeId { get; set; }
        public int DirectionId { get; set; }
    }

    public class StopSequenceDto
    {
        public int StopId { get; set; }
        public int StopSequence { get; set; }
    }
}
