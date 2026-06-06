using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using TursibBackend.Data;
using TursibBackend.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// --- JWT Key: prefer environment variable over appsettings ---
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured. Set JWT_KEY environment variable.");

// Add services to the container.
builder.Services.AddOpenApi();

// Configure Memory Cache
builder.Services.AddMemoryCache();

// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Configurare DbContext pentru Entity Framework Core cu SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<RouteCalculatorService>();
builder.Services.AddScoped<PaymentSimulatorService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Global policy: 100 requests per minute per IP
    options.AddPolicy("general", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Strict policy for auth endpoints: 10 requests per minute per IP
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Configurare CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:8080",
                  "http://localhost:5173",
                  "http://localhost:5174",
                  "https://tursib.onrender.com",
                  "https://tursib.vercel.app")
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS");
    });
});

// Adaugă suport pentru controllere
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// --- Security Headers ---
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// Enable response compression
app.UseResponseCompression();

// Activează politica CORS
app.UseCors("AllowVueApp");

// --- HTTPS Redirection (production only) ---
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Rate Limiting
app.UseRateLimiter();

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Mapează controllerele
app.MapControllers();

// Endpoint pentru import GTFS
app.MapPost("/api/import-gtfs", (IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        var gtfsPath = Environment.GetEnvironmentVariable("GTFS_PATH")
            ?? config["GtfsPath"]
            ?? throw new InvalidOperationException("GTFS path not configured");

        TursibBackend.RunGTFSImport.ExecuteImport(gtfsPath);
        return Results.Ok(new { message = "GTFS import completed successfully" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "GTFS import failed");
        return Results.Problem("Import failed. Check server logs for details.");
    }
})
.WithName("ImportGTFS")
.RequireAuthorization(policy => policy.RequireRole("Admin"));

// --- Debug endpoints: development only ---
if (app.Environment.IsDevelopment())
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=TursibDb.db";

    app.MapGet("/api/debug/routestations", () =>
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM RouteStations";
        var count = cmd.ExecuteScalar();

        cmd.CommandText = "SELECT RouteId, COUNT(*) as StationCount FROM RouteStations GROUP BY RouteId LIMIT 10";
        var results = new List<object>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                results.Add(new { routeId = reader.GetInt32(0), stationCount = reader.GetInt32(1) });
            }
        }

        return Results.Ok(new { totalRouteStations = count, byRoute = results });
    })
    .WithName("DebugRouteStations")
    .RequireAuthorization(policy => policy.RequireRole("Admin"));

    app.MapGet("/api/debug/gtfs", () =>
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connString);
        conn.Open();

        var info = new Dictionary<string, object>();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Routes";
        info["routes"] = cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT COUNT(*) FROM Stations";
        info["stations"] = cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT COUNT(*) FROM Trips";
        info["trips"] = cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT COUNT(*) FROM StopTimes";
        info["stopTimes"] = cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT COUNT(*) FROM Shapes";
        info["shapes"] = cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT TripId FROM Trips WHERE RouteId = 1 LIMIT 1";
        var tripId = cmd.ExecuteScalar()?.ToString();
        info["sampleTripRoute1"] = tripId ?? "none";

        if (!string.IsNullOrEmpty(tripId))
        {
            var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM StopTimes WHERE TripId = @tid";
            countCmd.Parameters.AddWithValue("@tid", tripId);
            info["stopsInSampleTrip"] = countCmd.ExecuteScalar()!;

            var stopsCmd = conn.CreateCommand();
            stopsCmd.CommandText = "SELECT StopId, StopSequence FROM StopTimes WHERE TripId = @tid ORDER BY StopSequence LIMIT 3";
            stopsCmd.Parameters.AddWithValue("@tid", tripId);
            var stops = new List<object>();
            using (var reader = stopsCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var stopId = reader.GetInt32(0);
                    var seq = reader.GetInt32(1);

                    var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM Stations WHERE Id = @sid";
                    checkCmd.Parameters.AddWithValue("@sid", stopId);
                    var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

                    stops.Add(new { stopId, sequence = seq, existsInStations = exists });
                }
            }
            info["sampleStops"] = stops;
        }

        return Results.Ok(info);
    })
    .WithName("DebugGTFS")
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
}

app.Run();

public partial class Program { }
