using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Data.Common;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Tests.Integration;

public class AuthRoutingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthRoutingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ShouldReturnJwtToken_WhenCredentialsAreValid()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "admin",
            password = "admin123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.TryGetProperty("token", out var tokenProperty).Should().BeTrue();
        tokenProperty.GetString().Should().NotBeNullOrWhiteSpace();

        payload.TryGetProperty("role", out var roleProperty).Should().BeTrue();
        roleProperty.GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenPasswordIsInvalid()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "admin",
            password = "invalid-password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Favorites_ShouldAllowAccess_WhenBearerTokenIsValid()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "admin",
            password = "admin123"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginPayload.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var favoritesResponse = await _client.GetAsync("/api/Favorites");

        favoritesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoutingAlternatives_ShouldReturnTopRoutes_WhenStationsAreConnected()
    {
        var response = await _client.PostAsJsonAsync("/api/routing/alternatives", new
        {
            startStationId = 1,
            endStationId = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.ValueKind.Should().Be(JsonValueKind.Array);
        payload.GetArrayLength().Should().BeGreaterThan(0);

        var firstRoute = payload.EnumerateArray().First();
        firstRoute.GetProperty("totalDuration").GetDouble().Should().BeGreaterThan(0);

        var segments = firstRoute.GetProperty("segments");
        segments.ValueKind.Should().Be(JsonValueKind.Array);
        segments.GetArrayLength().Should().BeGreaterThan(0);
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "IntegrationTests_SuperSecretKey_1234567890",
                ["Jwt:Issuer"] = "TursibTests",
                ["Jwt:Audience"] = "TursibTestsClient"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(DbConnection));

            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            services.AddSingleton<DbConnection>(connection);

            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                var connection = serviceProvider.GetRequiredService<DbConnection>();
                options.UseSqlite(connection);
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            SeedData(db);
        });
    }

    private static void SeedData(ApplicationDbContext db)
    {
        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Id = 100,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!db.Stations.Any())
        {
            db.Stations.AddRange(
                new Station { Id = 1, Name = "Start", Latitude = 45.790, Longitude = 24.140 },
                new Station { Id = 2, Name = "Mid", Latitude = 45.791, Longitude = 24.145 },
                new Station { Id = 3, Name = "End", Latitude = 45.792, Longitude = 24.150 }
            );
        }

        if (!db.Routes.Any())
        {
            db.Routes.Add(new Route
            {
                Id = 1,
                RouteNumber = "1",
                Name = "Start-End"
            });
        }

        if (!db.RouteStations.Any())
        {
            db.RouteStations.AddRange(
                new RouteStation { Id = 1, RouteId = 1, StationId = 1, Order = 1 },
                new RouteStation { Id = 2, RouteId = 1, StationId = 2, Order = 2 },
                new RouteStation { Id = 3, RouteId = 1, StationId = 3, Order = 3 }
            );
        }

        db.SaveChanges();
    }
}
