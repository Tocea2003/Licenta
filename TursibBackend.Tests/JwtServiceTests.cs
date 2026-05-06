using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using TursibBackend.Models;
using TursibBackend.Services;
using Xunit;

namespace TursibBackend.Tests;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"]      = "super-secret-key-that-is-at-least-32-chars-long!",
            ["Jwt:Issuer"]   = "TursibTest",
            ["Jwt:Audience"] = "TursibTestUsers"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    private static User MakeUser(string role = "User") => new()
    {
        Id       = 42,
        Username = "testuser",
        Role     = role,
        Email    = "test@example.com"
    };

    // ── GenerateToken ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = _jwtService.GenerateToken(MakeUser());
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_ProducesAThreePartJwt()
    {
        var token = _jwtService.GenerateToken(MakeUser());
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateToken_EmbeddsCorrectUsername()
    {
        var token = _jwtService.GenerateToken(MakeUser());
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var nameClaim = jwt.Claims.FirstOrDefault(c =>
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        Assert.Equal("testuser", nameClaim?.Value);
    }

    [Fact]
    public void GenerateToken_EmbeddsCorrectRole()
    {
        var token = _jwtService.GenerateToken(MakeUser("Admin"));
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var roleClaim = jwt.Claims.FirstOrDefault(c =>
            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        Assert.Equal("Admin", roleClaim?.Value);
    }

    [Fact]
    public void GenerateToken_EmbeddsCorrectUserId()
    {
        var token = _jwtService.GenerateToken(MakeUser());
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var subClaim = jwt.Claims.FirstOrDefault(c =>
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        Assert.Equal("42", subClaim?.Value);
    }

    [Fact]
    public void GenerateToken_ExpiresInApproximatelySevenDays()
    {
        var before = DateTime.UtcNow;
        var token  = _jwtService.GenerateToken(MakeUser());
        var after  = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var lower = before.AddDays(6).AddHours(23);
        var upper = after.AddDays(7).AddMinutes(1);
        Assert.InRange(jwt.ValidTo, lower, upper);
    }

    // ── ValidateToken ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_ReturnsClaimsPrincipalForValidToken()
    {
        var token     = _jwtService.GenerateToken(MakeUser());
        var principal = _jwtService.ValidateToken(token);

        Assert.NotNull(principal);
    }

    [Fact]
    public void ValidateToken_ReturnsNullForGarbageString()
    {
        var principal = _jwtService.ValidateToken("not.a.valid.jwt");
        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_ReturnsNullForTokenSignedWithDifferentKey()
    {
        // Build a service with a different secret
        var otherSettings = new Dictionary<string, string?>
        {
            ["Jwt:Key"]      = "completely-different-key-with-enough-chars-here!",
            ["Jwt:Issuer"]   = "TursibTest",
            ["Jwt:Audience"] = "TursibTestUsers"
        };
        var otherConfig  = new ConfigurationBuilder().AddInMemoryCollection(otherSettings).Build();
        var otherService = new JwtService(otherConfig);

        var tokenFromOtherService = otherService.GenerateToken(MakeUser());
        var principal = _jwtService.ValidateToken(tokenFromOtherService);

        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_ReturnsNullForEmptyString()
    {
        var principal = _jwtService.ValidateToken(string.Empty);
        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_ClaimsPrincipalContainsRoleClaim()
    {
        var token     = _jwtService.GenerateToken(MakeUser("Admin"));
        var principal = _jwtService.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.True(principal!.IsInRole("Admin"));
    }
}
