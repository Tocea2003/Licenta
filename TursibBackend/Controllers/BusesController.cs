// Controllers/BusesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BusesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BusesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/buses
        // Returnează toate autobuzele din sistem
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Bus>>> GetBuses()
        {
            // Includem și informații despre traseul curent al autobuzului
            return await _context.Buses
                .Include(b => b.CurrentRoute)
                .ToListAsync();
        }

        // GET: api/buses/5
        // Returnează un autobuz specific după ID
        [HttpGet("{id}")]
        public async Task<ActionResult<Bus>> GetBus(int id)
        {
            var bus = await _context.Buses
                .Include(b => b.CurrentRoute)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bus == null)
            {
                return NotFound(new { message = $"Autobuzul cu ID-ul {id} nu a fost găsit." });
            }

            return bus;
        }
    }
}
