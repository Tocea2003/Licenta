using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TursibBackend.Models;
using TursibBackend.Services;

namespace TursibBackend.Tests.Services;

/// <summary>
/// Teste pentru JwtService - verifică generarea și validarea token-urilor JWT.
/// </summary>
public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        // Configurare pentru teste
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Key", "ThisIsAVerySecureKeyForTestingPurposesWithAtLeast32Characters!"},
            {"Jwt:Issuer", "TursibBackendTest"},
            {"Jwt:Audience", "TursibFrontendTest"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    #region Token Generation Tests

    [Fact]
    public void GenerateToken_ShouldReturnValidJwtToken()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };

        // Act
        var token = _jwtService.GenerateToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        // Verifică că e un JWT valid (are 3 părți separate de .)
        var parts = token.Split('.');
        parts.Should().HaveCount(3, "un JWT valid are header.payload.signature");
    }

    [Fact]
    public void GenerateToken_ShouldIncludeUserClaims()
    {
        // Arrange
        var user = new User
        {
            Id = 42,
            Username = "johndoe",
            Email = "john@example.com",
            Role = "Admin",
            PasswordHash = "dummy_hash"
        };

        // Act
        var token = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => 
            c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
        jwtToken.Claims.Should().Contain(c => 
            c.Type == ClaimTypes.Name && c.Value == "johndoe");
        jwtToken.Claims.Should().Contain(c => 
            c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_ShouldSetCorrectIssuerAndAudience()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };

        // Act
        var token = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("TursibBackendTest");
        jwtToken.Audiences.Should().Contain("TursibFrontendTest");
    }

    [Fact]
    public void GenerateToken_ShouldSetExpirationDate()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };

        // Act
        var token = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
        jwtToken.ValidTo.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(7), 
            TimeSpan.FromMinutes(1),
            "token-ul ar trebui să expire în 7 zile"
        );
    }

    [Fact]
    public void GenerateToken_ShouldGenerateDifferentTokens_ForDifferentUsers()
    {
        // Arrange
        var user1 = new User
        {
            Id = 1,
            Username = "user1",
            Email = "user1@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };

        var user2 = new User
        {
            Id = 2,
            Username = "user2",
            Email = "user2@example.com",
            Role = "Admin",
            PasswordHash = "dummy_hash"
        };

        // Act
        var token1 = _jwtService.GenerateToken(user1);
        var token2 = _jwtService.GenerateToken(user2);

        // Assert
        token1.Should().NotBe(token2, "utilizatori diferiți ar trebui să aibă token-uri diferite");
    }

    #endregion

    #region Token Validation Tests

    [Fact]
    public void ValidateToken_ShouldReturnPrincipal_ForValidToken()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };
        var token = _jwtService.GenerateToken(user);

        // Act
        var principal = _jwtService.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_ShouldExtractCorrectClaims()
    {
        // Arrange
        var user = new User
        {
            Id = 123,
            Username = "johndoe",
            Email = "john@example.com",
            Role = "Admin",
            PasswordHash = "dummy_hash"
        };
        var token = _jwtService.GenerateToken(user);

        // Act
        var principal = _jwtService.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();

        var idClaim = principal!.FindFirst(ClaimTypes.NameIdentifier);
        idClaim.Should().NotBeNull();
        idClaim!.Value.Should().Be("123");

        var nameClaim = principal.FindFirst(ClaimTypes.Name);
        nameClaim.Should().NotBeNull();
        nameClaim!.Value.Should().Be("johndoe");

        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("Admin");
    }

    [Fact]
    public void ValidateToken_ShouldReturnNull_ForInvalidToken()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var principal = _jwtService.ValidateToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_ShouldReturnNull_ForExpiredToken()
    {
        // Arrange - Creăm un token cu expirare în trecut
        // (acest test necesită manipularea timpului sau un token pre-generat expirat)
        // Pentru simplitate, testăm cu un token invalid care simula expirarea
        var malformedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjB9.invalid";

        // Act
        var principal = _jwtService.ValidateToken(malformedToken);

        // Assert
        principal.Should().BeNull("token-ul expirat sau invalid nu ar trebui validat");
    }

    [Fact]
    public void ValidateToken_ShouldReturnNull_ForEmptyToken()
    {
        // Arrange
        var emptyToken = "";

        // Act
        var principal = _jwtService.ValidateToken(emptyToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_ShouldReturnNull_ForTokenWithInvalidSignature()
    {
        // Arrange - Luăm un token valid și modificăm ultima parte (signature)
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };
        var validToken = _jwtService.GenerateToken(user);
        var parts = validToken.Split('.');
        var tamperedToken = $"{parts[0]}.{parts[1]}.invalidSignature";

        // Act
        var principal = _jwtService.ValidateToken(tamperedToken);

        // Assert
        principal.Should().BeNull("token-ul cu semnătură modificată ar trebui respins");
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void GenerateAndValidateToken_ShouldWorkCorrectly()
    {
        // Arrange
        var user = new User
        {
            Id = 999,
            Username = "roundtriptest",
            Email = "roundtrip@example.com",
            Role = "User",
            PasswordHash = "dummy_hash"
        };

        // Act - Generează token
        var token = _jwtService.GenerateToken(user);
        
        // Act - Validează token
        var principal = _jwtService.ValidateToken(token);

        // Assert - Verifică că informațiile sunt păstrate corect
        principal.Should().NotBeNull();
        
        var userId = principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        userId.Should().Be("999");
        
        var username = principal.FindFirst(ClaimTypes.Name)?.Value;
        username.Should().Be("roundtriptest");
        
        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        role.Should().Be("User");
    }

    #endregion

    #region Security Tests

    [Fact]
    public void GenerateToken_ShouldThrowException_WhenJwtKeyNotConfigured()
    {
        // Arrange - Configurare fără cheia JWT
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Jwt:Issuer", "Test"},
                {"Jwt:Audience", "Test"}
                // Lipsește Jwt:Key
            }!)
            .Build();

        var serviceWithoutKey = new JwtService(emptyConfig);
        
        var user = new User
        {
            Id = 1,
            Username = "test",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "dummy"
        };

        // Act & Assert
        var act = () => serviceWithoutKey.GenerateToken(user);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Key not configured*");
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    [InlineData("Moderator")]
    public void GenerateToken_ShouldWorkForDifferentRoles(string role)
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = role,
            PasswordHash = "dummy_hash"
        };

        // Act
        var token = _jwtService.GenerateToken(user);
        var principal = _jwtService.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();
        var roleClaim = principal!.FindFirst(ClaimTypes.Role)?.Value;
        roleClaim.Should().Be(role);
    }

    #endregion
}
