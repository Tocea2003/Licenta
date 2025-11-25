// Controllers/RoutesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RoutesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/routes
        // Returnează toate traseele disponibile
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Models.Route>>> GetRoutes()
        {
            return await _context.Routes.ToListAsync();
        }

        // GET: api/routes/5
        // Returnează un traseu specific după ID
        [HttpGet("{id}")]
        public async Task<ActionResult<Models.Route>> GetRoute(int id)
        {
            var route = await _context.Routes.FindAsync(id);

            if (route == null)
            {
                return NotFound(new { message = $"Traseul cu ID-ul {id} nu a fost găsit." });
            }

            return route;
        }

        // 🚨 ENDPOINT CRITIC 🚨
        // GET: api/routes/5/stations
        // Returnează stațiile unui traseu, ORDONATE după câmpul "Order"
        [HttpGet("{id}/stations")]
        public async Task<ActionResult<IEnumerable<Station>>> GetRouteStations(int id)
        {
            // Verificăm dacă traseul există
            var routeExists = await _context.Routes.AnyAsync(r => r.Id == id);
            if (!routeExists)
            {
                return NotFound(new { message = $"Traseul cu ID-ul {id} nu există." });
            }

            // Interogăm RouteStations pentru a obține stațiile ordonate
            var stations = await _context.RouteStations
                .Where(rs => rs.RouteId == id)        // Filtrăm după traseul dorit
                .Include(rs => rs.Station)            // Includem datele stației
                .OrderBy(rs => rs.Order)              // 🔥 ORDONĂM după câmpul Order
                .Select(rs => rs.Station)             // Extragem doar obiectul Station
                .ToListAsync();

            return Ok(stations);
        }
    }
}
