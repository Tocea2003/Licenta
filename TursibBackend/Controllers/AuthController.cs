using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;

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

        // POST: api/Auth/google
        [HttpPost("google")]
        public async Task<ActionResult<LoginResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                // Verifică și validează token-ul Google
                var googleUser = await ValidateGoogleToken(request.Credential);
                
                if (googleUser == null)
                {
                    return Unauthorized(new { message = "Token Google invalid" });
                }

                // Caută utilizatorul după email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == googleUser.Email);

                // Dacă utilizatorul nu există, îl creăm
                if (user == null)
                {
                    // Generează un username unic bazat pe email
                    var baseUsername = googleUser.Email.Split('@')[0];
                    var username = baseUsername;
                    var counter = 1;
                    
                    while (await _context.Users.AnyAsync(u => u.Username == username))
                    {
                        username = $"{baseUsername}{counter}";
                        counter++;
                    }

                    // Verifică dacă este primul user (devine Admin)
                    var isFirstUser = !await _context.Users.AnyAsync();

                    user = new User
                    {
                        Username = username,
                        Email = googleUser.Email,
                        PasswordHash = "", // Google users nu au parolă locală
                        Role = isFirstUser ? "Admin" : "User",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✅ Created new Google user: {user.Username} ({user.Email})");
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
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in Google login: {ex.Message}");
                return StatusCode(500, new { message = "Eroare la autentificare cu Google" });
            }
        }

        private async Task<GoogleUserInfo?> ValidateGoogleToken(string credential)
        {
            try
            {
                // Decodifică JWT token-ul de la Google
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(credential);

                // Extrage informațiile utilizatorului
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var name = jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                var picture = jwtToken.Claims.FirstOrDefault(c => c.Type == "picture")?.Value;
                var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sub))
                {
                    return null;
                }

                return new GoogleUserInfo
                {
                    Email = email,
                    Name = name ?? email,
                    Picture = picture ?? "",
                    GoogleId = sub
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error validating Google token: {ex.Message}");
                return null;
            }
        }
    }

    public class GoogleLoginRequest
    {
        public string Credential { get; set; } = "";
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Picture { get; set; } = "";
        public string GoogleId { get; set; } = "";
    }
}
