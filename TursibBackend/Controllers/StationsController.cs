// Controllers/StationsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TursibBackend.Data;
using TursibBackend.Models;
using System.Diagnostics;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<StationsController> _logger;
        private const int CACHE_DURATION_MINUTES = 30;

        public StationsController(ApplicationDbContext context, IMemoryCache cache, ILogger<StationsController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // GET: api/stations
        // Returnează toate stațiile disponibile în sistem (cu caching)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Station>>> GetStations()
        {
            var stopwatch = Stopwatch.StartNew();
            const string cacheKey = "all_stations";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out List<Station>? cachedStations))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for stations - Response time: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return cachedStations!;
            }

            // Cache MISS - interogare database
            _logger.LogInformation("⚠️ Cache MISS for stations - Querying database...");
            var stations = await _context.Stations.ToListAsync();
            
            // Salvează în cache
            _cache.Set(cacheKey, stations, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Stations loaded from DB - Response time: {ElapsedMs}ms, Count: {Count} (cached for {Minutes}min)", 
                stopwatch.ElapsedMilliseconds, stations.Count, CACHE_DURATION_MINUTES);
            
            return stations;
        }

        // GET: api/stations/5
        // Returnează o stație specifică după ID (cu caching)
        [HttpGet("{id}")]
        public async Task<ActionResult<Station>> GetStation(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"station_{id}";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out Station? cachedStation))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for station {StationId} - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return cachedStation!;
            }

            // Cache MISS - interogare database
            var station = await _context.Stations.FindAsync(id);

            if (station == null)
            {
                stopwatch.Stop();
                _logger.LogWarning("❌ Station {StationId} not found - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return NotFound(new { message = $"Stația cu ID-ul {id} nu a fost găsită." });
            }

            // Salvează în cache
            _cache.Set(cacheKey, station, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Station {StationId} loaded from DB - Response time: {ElapsedMs}ms (cached for {Minutes}min)", 
                id, stopwatch.ElapsedMilliseconds, CACHE_DURATION_MINUTES);

            return station;
        }

        // GET: api/stations/5/routes
        // Returnează traseele care trec prin stație, verificate direct din BD
        [HttpGet("{id}/routes")]
        public async Task<ActionResult<IEnumerable<TursibBackend.Models.Route>>> GetStationRoutes(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"station_{id}_routes";

            if (_cache.TryGetValue(cacheKey, out List<TursibBackend.Models.Route>? cachedRoutes))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for station {StationId} routes - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return cachedRoutes!;
            }

            var stationExists = await _context.Stations.AnyAsync(s => s.Id == id);
            if (!stationExists)
            {
                stopwatch.Stop();
                return NotFound(new { message = $"Stația cu ID-ul {id} nu a fost găsită." });
            }

            var routes = await _context.RouteStations
                .Where(rs => rs.StationId == id)
                .Include(rs => rs.Route)
                .Select(rs => rs.Route)
                .Distinct()
                .OrderBy(r => r.RouteNumber)
                .ToListAsync();

            _cache.Set(cacheKey, routes, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            stopwatch.Stop();
            _logger.LogInformation("📊 Station {StationId} routes loaded from DB - Response time: {ElapsedMs}ms, Count: {Count}", 
                id, stopwatch.ElapsedMilliseconds, routes.Count);

            return Ok(routes);
        }

        // GET: api/stations/5/schedule
        // Returnează orarul stației (ore de sosire) din tabelul StopTimes, grupate pe traseu și direcție
        [HttpGet("{id}/schedule")]
        public async Task<ActionResult<IEnumerable<object>>> GetStationSchedule(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"station_{id}_schedule";

            if (_cache.TryGetValue(cacheKey, out List<StationScheduleEntry>? cachedSchedule))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for station {StationId} schedule - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return Ok(cachedSchedule!);
            }

            var stationExists = await _context.Stations.AnyAsync(s => s.Id == id);
            if (!stationExists)
            {
                stopwatch.Stop();
                return NotFound(new { message = $"Stația cu ID-ul {id} nu a fost găsită." });
            }

            var schedule = await (
                from st in _context.StopTimes.AsNoTracking()
                join t in _context.Trips.AsNoTracking() on st.TripId equals t.TripId
                join r in _context.Routes.AsNoTracking() on t.RouteId equals r.Id
                where st.StopId == id
                orderby r.RouteNumber, t.DirectionId, st.DepartureTime
                select new StationScheduleEntry
                {
                    RouteId = r.Id,
                    RouteNumber = r.RouteNumber,
                    RouteName = r.Name,
                    RouteColor = r.Color,
                    Direction = t.TripHeadsign,
                    DirectionId = t.DirectionId ?? 0,
                    ArrivalTime = st.ArrivalTime,
                    DepartureTime = st.DepartureTime
                }).ToListAsync();

            _cache.Set(cacheKey, schedule, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            stopwatch.Stop();
            _logger.LogInformation("📊 Station {StationId} schedule loaded from DB - Response time: {ElapsedMs}ms, Count: {Count}", 
                id, stopwatch.ElapsedMilliseconds, schedule.Count);

            return Ok(schedule);
        }
    }

    // DTO pentru orarul stației
    public class StationScheduleEntry
    {
        public int RouteId { get; set; }
        public string RouteNumber { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public string? RouteColor { get; set; }
        public string? Direction { get; set; }
        public int DirectionId { get; set; }
        public string ArrivalTime { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
    }
}
