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
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, JwtService jwtService, IConfiguration configuration)
        {
            _context = context;
            _jwtService = jwtService;
            _configuration = configuration;
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
                GoogleUserInfo? googleUser;

                if (!string.IsNullOrEmpty(request.Code))
                {
                    googleUser = await ExchangeCodeForUser(request.Code, request.RedirectUri ?? "");
                }
                else if (!string.IsNullOrEmpty(request.Credential))
                {
                    googleUser = await ValidateGoogleToken(request.Credential);
                }
                else
                {
                    return BadRequest(new { message = "Code or credential required" });
                }

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
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Console.WriteLine($"❌ Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, new { message = "Eroare la autentificare cu Google", error = ex.Message });
            }
        }

        private async Task<GoogleUserInfo?> ExchangeCodeForUser(string code, string redirectUri)
        {
            try
            {
                var clientId = _configuration["Google:ClientId"]
                    ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
                var clientSecret = _configuration["Google:ClientSecret"]
                    ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";

                using var httpClient = new HttpClient();
                var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["code"] = code,
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret,
                        ["redirect_uri"] = redirectUri,
                        ["grant_type"] = "authorization_code"
                    }));

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Google token exchange failed: {tokenJson}");
                    return null;
                }

                var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
                var idToken = tokenData.GetProperty("id_token").GetString();

                if (string.IsNullOrEmpty(idToken))
                    return null;

                return await ValidateGoogleToken(idToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error exchanging Google code: {ex.Message}");
                return null;
            }
        }

        private async Task<GoogleUserInfo?> ValidateGoogleToken(string credential)
        {
            try
            {
                // Validează token-ul cu cheile publice Google (verifică semnătura)
                var handler = new JwtSecurityTokenHandler();
                var googleClientId = _configuration["Google:ClientId"]
                    ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                    ?? "";

                // Descarcă cheile publice Google pentru verificarea semnăturii
                using var httpClient = new HttpClient();
                var jwksJson = await httpClient.GetStringAsync("https://www.googleapis.com/oauth2/v3/certs");
                var jwks = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwksJson);

                var validationParams = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = new[] { "accounts.google.com", "https://accounts.google.com" },
                    ValidateAudience = !string.IsNullOrEmpty(googleClientId),
                    ValidAudience = googleClientId,
                    ValidateLifetime = true,
                    IssuerSigningKeys = jwks.Keys
                };

                var principal = handler.ValidateToken(credential, validationParams, out _);

                var email = principal.FindFirst("email")?.Value
                    ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var name = principal.FindFirst("name")?.Value
                    ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var picture = principal.FindFirst("picture")?.Value;
                var sub = principal.FindFirst("sub")?.Value
                    ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

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
        public string Code { get; set; } = "";
        public string? RedirectUri { get; set; }
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Picture { get; set; } = "";
        public string GoogleId { get; set; } = "";
    }
}
