using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TursibBackend.Data;
using TursibBackend.Models;
using Xunit;

namespace TursibBackend.Tests;

/// <summary>
/// Integration tests for AuthController using WebApplicationFactory with an
/// in-memory database. Each test class instance gets its own isolated database.
/// </summary>
public class AuthControllerTests : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(AuthWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [Fact]
    public async Task Register_Returns201_WithValidCredentials()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "newuser",
            password = "password123"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsUserDetails_WithoutPasswordHash()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "detailuser",
            password = "password123"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("username", out _), "Response should contain 'username'");
        Assert.True(body.TryGetProperty("role", out _), "Response should contain 'role'");
        Assert.False(body.TryGetProperty("passwordHash", out _), "Response must not expose passwordHash");
    }

    [Fact]
    public async Task Register_Returns400_WhenUsernameIsTooShort()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "ab",   // < 3 chars
            password = "password123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Returns400_WhenPasswordIsTooShort()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "validuser",
            password = "12345"  // < 6 chars
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Returns400_WhenUsernameAlreadyExists()
    {
        // First registration
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "duplicateuser",
            password = "password123"
        });

        // Second registration with same username
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "duplicateuser",
            password = "differentpassword"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_FirstUser_ReceivesAdminRole()
    {
        // The factory creates a fresh DB, so the very first registration should get Admin
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "firstadmin",
            password = "password123"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Admin", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_SecondUser_ReceivesUserRole()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "firstadmin2",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "regularuser",
            password = "password123"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User", body.GetProperty("role").GetString());
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_Returns200_WithCorrectCredentials()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "loginuser",
            password = "correctpassword"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "loginuser",
            password = "correctpassword"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsToken_OnSuccess()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "tokenuser",
            password = "correctpassword"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "tokenuser",
            password = "correctpassword"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token!.Split('.').Length);
    }

    [Fact]
    public async Task Login_Returns401_WithWrongPassword()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "wrongpassuser",
            password = "correctpassword"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "wrongpassuser",
            password = "wrongpassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Returns401_ForNonExistentUsername()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "ghostuser",
            password = "anypassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ResponseContainsUsernameAndRole()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "infouser",
            password = "correctpassword"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "infouser",
            password = "correctpassword"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("infouser", body.GetProperty("username").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("role").GetString()));
    }
}

/// <summary>
/// Custom WebApplicationFactory that replaces the SQLite database with an
/// isolated in-memory database for each test class instance.
/// </summary>
public class AuthWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DB context registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add in-memory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Ensure the database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]      = "integration-test-key-that-is-long-enough-for-hs256!",
                ["Jwt:Issuer"]   = "TursibIntegrationTest",
                ["Jwt:Audience"] = "TursibIntegrationTestUsers"
            });
        });
    }
}
