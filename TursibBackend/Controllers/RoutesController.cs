// Controllers/RoutesController.cs
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
    public class RoutesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RoutesController> _logger;
        private const int CACHE_DURATION_MINUTES = 30;

        public RoutesController(ApplicationDbContext context, IMemoryCache cache, ILogger<RoutesController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // GET: api/routes
        // Returnează toate traseele disponibile (cu caching)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Models.Route>>> GetRoutes()
        {
            var stopwatch = Stopwatch.StartNew();
            const string cacheKey = "all_routes";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out List<Models.Route>? cachedRoutes))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for routes - Response time: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return cachedRoutes!;
            }

            // Cache MISS - interogare database
            _logger.LogInformation("⚠️ Cache MISS for routes - Querying database...");
            var routes = await _context.Routes.ToListAsync();
            
            // Salvează în cache pentru 30 minute
            _cache.Set(cacheKey, routes, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Database query completed - Response time: {ElapsedMs}ms (cached for {Minutes}min)", 
                stopwatch.ElapsedMilliseconds, CACHE_DURATION_MINUTES);
            
            return routes;
        }

        // GET: api/routes/5
        // Returnează un traseu specific după ID (cu caching)
        [HttpGet("{id}")]
        public async Task<ActionResult<Models.Route>> GetRoute(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"route_{id}";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out Models.Route? cachedRoute))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for route {RouteId} - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return cachedRoute!;
            }

            // Cache MISS - interogare database
            var route = await _context.Routes.FindAsync(id);

            if (route == null)
            {
                stopwatch.Stop();
                _logger.LogWarning("❌ Route {RouteId} not found - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return NotFound(new { message = $"Traseul cu ID-ul {id} nu a fost găsit." });
            }

            // Salvează în cache
            _cache.Set(cacheKey, route, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Route {RouteId} loaded from DB - Response time: {ElapsedMs}ms (cached for {Minutes}min)", 
                id, stopwatch.ElapsedMilliseconds, CACHE_DURATION_MINUTES);

            return route;
        }

        // 🚨 ENDPOINT CRITIC 🚨
        // GET: api/routes/5/stations
        // Returnează stațiile unui traseu, ORDONATE după câmpul "Order" (cu caching)
        [HttpGet("{id}/stations")]
        public async Task<ActionResult<IEnumerable<Station>>> GetRouteStations(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"route_{id}_stations";

            // Încearcă să obții din cache
            if (_cache.TryGetValue(cacheKey, out List<Station>? cachedStations))
            {
                stopwatch.Stop();
                _logger.LogInformation("✅ Cache HIT for route {RouteId} stations - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return cachedStations!;
            }

            // Cache MISS - interogare database
            _logger.LogInformation("⚠️ Cache MISS for route {RouteId} stations - Querying database...", id);
            
            // Verificăm dacă traseul există
            var routeExists = await _context.Routes.AnyAsync(r => r.Id == id);
            if (!routeExists)
            {
                stopwatch.Stop();
                _logger.LogWarning("❌ Route {RouteId} not found - Response time: {ElapsedMs}ms", id, stopwatch.ElapsedMilliseconds);
                return NotFound(new { message = $"Traseul cu ID-ul {id} nu există." });
            }

            // Interogăm RouteStations pentru a obține stațiile ordonate
            var stations = await _context.RouteStations
                .Where(rs => rs.RouteId == id)        // Filtrăm după traseul dorit
                .Include(rs => rs.Station)            // Includem datele stației
                .OrderBy(rs => rs.Order)              // 🔥 ORDONĂM după câmpul Order
                .Select(rs => rs.Station)             // Extragem doar obiectul Station
                .ToListAsync();

            // Salvează în cache
            _cache.Set(cacheKey, stations, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            stopwatch.Stop();
            _logger.LogInformation("📊 Route {RouteId} stations loaded from DB - Response time: {ElapsedMs}ms, Count: {Count} (cached for {Minutes}min)", 
                id, stopwatch.ElapsedMilliseconds, stations.Count, CACHE_DURATION_MINUTES);

            return Ok(stations);
        }
    }
}
