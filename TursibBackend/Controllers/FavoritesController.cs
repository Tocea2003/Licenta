using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using System.Text.Json;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavoritesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Favorites
        [HttpGet]
        public async Task<ActionResult<List<FavoriteLocation>>> GetFavorites()
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized(new { message = "Invalid or missing token" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (string.IsNullOrEmpty(user.Favorites))
            {
                return Ok(new List<FavoriteLocation>());
            }

            try
            {
                var favorites = JsonSerializer.Deserialize<List<FavoriteLocation>>(user.Favorites);
                return Ok(favorites ?? new List<FavoriteLocation>());
            }
            catch
            {
                return Ok(new List<FavoriteLocation>());
            }
        }

        // POST: api/Favorites
        [HttpPost]
        public async Task<ActionResult<FavoriteLocation>> AddFavorite([FromBody] FavoriteLocation favorite)
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized(new { message = "Invalid or missing token" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Parse existing favorites
            List<FavoriteLocation> favorites;
            if (string.IsNullOrEmpty(user.Favorites))
            {
                favorites = new List<FavoriteLocation>();
            }
            else
            {
                try
                {
                    favorites = JsonSerializer.Deserialize<List<FavoriteLocation>>(user.Favorites) ?? new List<FavoriteLocation>();
                }
                catch
                {
                    favorites = new List<FavoriteLocation>();
                }
            }

            // Check if it's a non-custom type and remove existing one
            if (favorite.Type != "custom")
            {
                favorites.RemoveAll(f => f.Type == favorite.Type);
            }

            // Generate ID if not provided
            if (string.IsNullOrEmpty(favorite.Id))
            {
                favorite.Id = $"fav_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N").Substring(0, 9)}";
            }

            favorite.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Add new favorite
            favorites.Add(favorite);

            // Save to database
            user.Favorites = JsonSerializer.Serialize(favorites);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFavorites), favorite);
        }

        // DELETE: api/Favorites/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFavorite(string id)
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized(new { message = "Invalid or missing token" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (string.IsNullOrEmpty(user.Favorites))
            {
                return NotFound(new { message = "Favorite not found" });
            }

            List<FavoriteLocation> favorites;
            try
            {
                favorites = JsonSerializer.Deserialize<List<FavoriteLocation>>(user.Favorites) ?? new List<FavoriteLocation>();
            }
            catch
            {
                return NotFound(new { message = "Favorite not found" });
            }

            var favorite = favorites.FirstOrDefault(f => f.Id == id);
            if (favorite == null)
            {
                return NotFound(new { message = "Favorite not found" });
            }

            favorites.Remove(favorite);

            // Save to database
            user.Favorites = favorites.Count > 0 ? JsonSerializer.Serialize(favorites) : null;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Favorites/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFavorite(string id, [FromBody] FavoriteLocation updatedFavorite)
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized(new { message = "Invalid or missing token" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (string.IsNullOrEmpty(user.Favorites))
            {
                return NotFound(new { message = "Favorite not found" });
            }

            List<FavoriteLocation> favorites;
            try
            {
                favorites = JsonSerializer.Deserialize<List<FavoriteLocation>>(user.Favorites) ?? new List<FavoriteLocation>();
            }
            catch
            {
                return NotFound(new { message = "Favorite not found" });
            }

            var index = favorites.FindIndex(f => f.Id == id);
            if (index == -1)
            {
                return NotFound(new { message = "Favorite not found" });
            }

            // Keep original ID and CreatedAt
            var original = favorites[index];
            updatedFavorite.Id = original.Id;
            updatedFavorite.CreatedAt = original.CreatedAt;

            favorites[index] = updatedFavorite;

            // Save to database
            user.Favorites = JsonSerializer.Serialize(favorites);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Favorites
        [HttpDelete]
        public async Task<IActionResult> ClearFavorites()
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized(new { message = "Invalid or missing token" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            user.Favorites = null;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Helper method to get username from Authorization header
        private string? GetUsernameFromToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                
                // ClaimTypes.Name se mapează la "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
                var usernameClaim = jwtToken.Claims.FirstOrDefault(c => 
                    c.Type == System.Security.Claims.ClaimTypes.Name || 
                    c.Type == "unique_name" ||
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
                return usernameClaim?.Value;
            }
            catch
            {
                return null;
            }
        }
    }
}
