using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using BCrypt.Net;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;

        public AuthController(ApplicationDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // Găsește utilizatorul după username
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                return Unauthorized(new { message = "Username sau parolă incorectă" });
            }

            // Verifică parola
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Username sau parolă incorectă" });
            }

            // Generează JWT token
            var token = _jwtService.GenerateToken(user);
            var expiresAt = DateTime.UtcNow.AddDays(7);

            return Ok(new LoginResponse
            {
                Token = token,
                Username = user.Username,
                Role = user.Role,
                ExpiresAt = expiresAt
            });
        }

        // POST: api/Auth/register
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromBody] LoginRequest request)
        {
            // Verifică dacă username-ul există deja
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest(new { message = "Username-ul există deja" });
            }

            // Validare username și parolă
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            {
                return BadRequest(new { message = "Username-ul trebuie să aibă minim 3 caractere" });
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return BadRequest(new { message = "Parola trebuie să aibă minim 6 caractere" });
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Verifică dacă este primul user (devine Admin)
            var isFirstUser = !await _context.Users.AnyAsync();

            var user = new User
            {
                Username = request.Username,
                PasswordHash = passwordHash,
                Role = isFirstUser ? "Admin" : "User", // Primul user devine Admin, restul User
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Login), new { id = user.Id }, new
            {
                user.Id,
                user.Username,
                user.Role
            });
        }
    }
}
