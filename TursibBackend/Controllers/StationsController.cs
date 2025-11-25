// Controllers/StationsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/stations
        // Returnează toate stațiile disponibile în sistem
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Station>>> GetStations()
        {
            return await _context.Stations.ToListAsync();
        }

        // GET: api/stations/5
        // Returnează o stație specifică după ID
        [HttpGet("{id}")]
        public async Task<ActionResult<Station>> GetStation(int id)
        {
            var station = await _context.Stations.FindAsync(id);

            if (station == null)
            {
                return NotFound(new { message = $"Stația cu ID-ul {id} nu a fost găsită." });
            }

            return station;
        }
    }
}
