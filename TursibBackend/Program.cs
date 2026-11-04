using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using TursibBackend.Data;
using TursibBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// --- JWT Key: prefer environment variable over appsettings ---
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured. Set JWT_KEY environment variable.");

// Inject the resolved key back into configuration so JwtService can read it
builder.Configuration["Jwt:Key"] = jwtKey;

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

// Configurare DbContext pentru Entity Framework Core cu PostgreSQL
// (local: Postgres din Homebrew/Docker, producție: Supabase via ConnectionStrings__DefaultConnection)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Connection string not configured. Set ConnectionStrings__DefaultConnection (env) or user-secrets.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

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

// Configurare CORS: exact origins only (no wildcard/substring matching)
var allowedOrigins = new List<string>
{
    "https://licenta-six-sand.vercel.app",  // production frontend
    "https://localhost",                    // Capacitor Android
    "capacitor://localhost",                // Capacitor iOS
};
if (builder.Environment.IsDevelopment())
{
    allowedOrigins.AddRange(new[] { "http://localhost:8080", "http://localhost:5173", "http://localhost:5174" });
}
// Extra origins (e.g. Vercel preview URLs), comma-separated
var extraOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
if (!string.IsNullOrWhiteSpace(extraOrigins))
{
    allowedOrigins.AddRange(extraOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");
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

// Apply pending migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Migrations only apply to relational providers (tests use InMemory)
    if (db.Database.IsRelational())
        db.Database.Migrate();

    // Comenzi de întreținere din linia de comandă (rulează și se opresc):
    //   dotnet run -- copy-from-sqlite <cale TursibDb.db>
    //   dotnet run -- import-gtfs <folder GTFS>
    if (args.Length >= 2 && args[0] is "copy-from-sqlite" or "import-gtfs")
    {
        if (args[0] == "copy-from-sqlite")
        {
            Console.WriteLine($"📦 Copying data from {args[1]} ...");
            await DatabaseMaintenance.CopyFromSqliteAsync(db, args[1]);
        }
        else
        {
            await new TursibBackend.GTFSImporter(db, args[1]).ImportAllAsync();
        }
        Console.WriteLine("✅ Done.");
        return;
    }

    // Bootstrap admin account from environment (only if no admin exists yet)
    var adminUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
    if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword)
        && !db.Users.Any(u => u.Role == "Admin"))
    {
        var existing = db.Users.FirstOrDefault(u => u.Username == adminUsername);
        if (existing != null)
        {
            existing.Role = "Admin";
            existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
        }
        else
        {
            db.Users.Add(new TursibBackend.Models.User
            {
                Username = adminUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "Admin"
            });
        }
        db.SaveChanges();
    }
}

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
app.MapPost("/api/import-gtfs", async (ApplicationDbContext db, IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        var gtfsPath = Environment.GetEnvironmentVariable("GTFS_PATH")
            ?? config["GtfsPath"]
            ?? throw new InvalidOperationException("GTFS path not configured");

        await new TursibBackend.GTFSImporter(db, gtfsPath).ImportAllAsync();
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
    app.MapGet("/api/debug/routestations", async (ApplicationDbContext db) =>
    {
        var total = await db.RouteStations.CountAsync();
        var byRoute = await db.RouteStations
            .GroupBy(rs => rs.RouteId)
            .Select(g => new { routeId = g.Key, stationCount = g.Count() })
            .OrderBy(x => x.routeId)
            .Take(10)
            .ToListAsync();

        return Results.Ok(new { totalRouteStations = total, byRoute });
    })
    .WithName("DebugRouteStations")
    .RequireAuthorization(policy => policy.RequireRole("Admin"));

    app.MapGet("/api/debug/gtfs", async (ApplicationDbContext db) =>
    {
        return Results.Ok(new
        {
            routes = await db.Routes.CountAsync(),
            stations = await db.Stations.CountAsync(),
            trips = await db.Trips.CountAsync(),
            stopTimes = await db.StopTimes.CountAsync(),
            shapes = await db.ShapePoints.CountAsync(),
            serviceCalendars = await db.ServiceCalendars.CountAsync()
        });
    })
    .WithName("DebugGTFS")
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
}

app.Run();

public partial class Program { }
