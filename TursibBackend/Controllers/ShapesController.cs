using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TursibBackend.Data;
using System.Diagnostics;

namespace TursibBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShapesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ShapesController> _logger;
        private const int CACHE_DURATION_MINUTES = 30;

        public ShapesController(ApplicationDbContext context, IMemoryCache cache, ILogger<ShapesController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // GET: api/shapes/{shapeId}
        [HttpGet("{shapeId}")]
        public async Task<ActionResult<IEnumerable<ShapePointDto>>> GetShapePoints(string shapeId)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"shape_{shapeId}";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out List<ShapePointDto>? cachedPoints))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for shape {ShapeId} - Response time: {ElapsedMs}ms", shapeId, stopwatch.ElapsedMilliseconds);
                return Ok(cachedPoints);
            }

            // Cache MISS - interogare database
            var shapePoints = await _context.Database
                .SqlQueryRaw<ShapePointDto>(@"
                    SELECT Latitude, Longitude, Sequence 
                    FROM Shapes 
                    WHERE ShapeId = {0}
                    ORDER BY Sequence", shapeId)
                .ToListAsync();

            if (!shapePoints.Any())
            {
                stopwatch.Stop();
                _logger.LogWarning("❌ Shape {ShapeId} not found - Response time: {ElapsedMs}ms", shapeId, stopwatch.ElapsedMilliseconds);
                return NotFound();
            }

            // Salvează în cache
            _cache.Set(cacheKey, shapePoints, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Shape {ShapeId} loaded from DB - Response time: {ElapsedMs}ms, Points: {Count} (cached for {Minutes}min)", 
                shapeId, stopwatch.ElapsedMilliseconds, shapePoints.Count, CACHE_DURATION_MINUTES);

            return Ok(shapePoints);
        }

        // GET: api/shapes/route/{routeId}
        [HttpGet("route/{routeId}")]
        public async Task<ActionResult<object>> GetRouteShape(int routeId)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"route_{routeId}_shape";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out object? cachedShape))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for route {RouteId} shape - Response time: {ElapsedMs}ms", routeId, stopwatch.ElapsedMilliseconds);
                return Ok(cachedShape);
            }

            // Cache MISS - interogare database
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
                stopwatch.Stop();
                _logger.LogWarning("❌ Route {RouteId} shape not found - Response time: {ElapsedMs}ms", routeId, stopwatch.ElapsedMilliseconds);
                return NotFound();
            }

            var shapePoints = await _context.Database
                .SqlQueryRaw<ShapePointDto>(@"
                    SELECT Latitude, Longitude, Sequence 
                    FROM Shapes 
                    WHERE ShapeId = {0}
                    ORDER BY Sequence", trip.ShapeId)
                .ToListAsync();

            var result = new
            {
                routeId,
                shapeId = trip.ShapeId,
                directionId = trip.DirectionId,
                points = shapePoints
            };

            // Salvează în cache
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Route {RouteId} shape loaded from DB - Response time: {ElapsedMs}ms, Points: {Count} (cached for {Minutes}min)", 
                routeId, stopwatch.ElapsedMilliseconds, shapePoints.Count, CACHE_DURATION_MINUTES);

            return Ok(result);
        }

        // GET: api/shapes/route/{routeId}/segment?fromStationId={fromId}&toStationId={toId}
        [HttpGet("route/{routeId}/segment")]
        public async Task<ActionResult<object>> GetRouteSegment(
            int routeId, 
            [FromQuery] int fromStationId, 
            [FromQuery] int toStationId)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"route_{routeId}_segment_{fromStationId}_{toStationId}";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out object? cachedSegment))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for route {RouteId} segment {From}->{To} - Response time: {ElapsedMs}ms", 
                    routeId, fromStationId, toStationId, stopwatch.ElapsedMilliseconds);
                return Ok(cachedSegment);
            }

            // Cache MISS - interogare database
            // Găsește un trip care conține AMBELE stații (nu LIMIT 1 arbitrar)
            var trip = await _context.Database
                .SqlQueryRaw<TripDto>(@"
                    SELECT t.TripId, t.ShapeId, t.DirectionId
                    FROM Trips t
                    INNER JOIN StopTimes st1 ON st1.TripId = t.TripId AND st1.StopId = {1}
                    INNER JOIN StopTimes st2 ON st2.TripId = t.TripId AND st2.StopId = {2}
                    WHERE t.RouteId = {0}
                    LIMIT 1", routeId, fromStationId, toStationId)
                .FirstOrDefaultAsync();

            if (trip == null || string.IsNullOrEmpty(trip.ShapeId))
            {
                stopwatch.Stop();
                _logger.LogInformation("Trip not found for route {RouteId} with stations {From}->{To} - Response time: {ElapsedMs}ms", routeId, fromStationId, toStationId, stopwatch.ElapsedMilliseconds);
                return NoContent();
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
                stopwatch.Stop();
                _logger.LogWarning("❌ Stations not found in trip for route {RouteId} - Response time: {ElapsedMs}ms", routeId, stopwatch.ElapsedMilliseconds);
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
            var result = new
            {
                routeId,
                shapeId = trip.ShapeId,
                fromStationId,
                toStationId,
                points = allShapePoints
            };

            // Salvează în cache
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Route {RouteId} segment {From}->{To} loaded from DB - Response time: {ElapsedMs}ms, Points: {Count} (cached for {Minutes}min)", 
                routeId, fromStationId, toStationId, stopwatch.ElapsedMilliseconds, allShapePoints.Count, CACHE_DURATION_MINUTES);

            return Ok(result);
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
