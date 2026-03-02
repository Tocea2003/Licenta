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
    }
}
