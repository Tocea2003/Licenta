// Controllers/StationsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TursibBackend.Data;
using TursibBackend.Models;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

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

            // Interogăm StopTimes și Trips via SQL raw deoarece aceste tabele
            // sunt create de GTFSImporter și nu sunt gestionate de EF Core
            var connectionString = _context.Database.GetConnectionString();
            var schedule = new List<StationScheduleEntry>();

            using (var conn = new SqliteConnection(connectionString))
            {
                await conn.OpenAsync();
                var sql = @"
                    SELECT 
                        r.Id AS RouteId,
                        r.RouteNumber,
                        r.Name AS RouteName,
                        r.Color AS RouteColor,
                        t.TripHeadsign AS Direction,
                        t.DirectionId,
                        st.ArrivalTime,
                        st.DepartureTime
                    FROM StopTimes st
                    JOIN Trips t ON st.TripId = t.TripId
                    JOIN Routes r ON t.RouteId = r.Id
                    WHERE st.StopId = @StopId
                    ORDER BY r.RouteNumber, t.DirectionId, st.DepartureTime";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new SqliteParameter("@StopId", id));

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            schedule.Add(new StationScheduleEntry
                            {
                                RouteId = reader.GetInt32(0),
                                RouteNumber = reader.GetString(1),
                                RouteName = reader.GetString(2),
                                RouteColor = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Direction = reader.IsDBNull(4) ? null : reader.GetString(4),
                                DirectionId = reader.GetInt32(5),
                                ArrivalTime = reader.GetString(6),
                                DepartureTime = reader.GetString(7)
                            });
                        }
                    }
                }
            }

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
