using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using TursibBackend.Data;
using TursibBackend.Services;

// Verificarea unui feed GTFS fără bază de date, fără JWT_KEY, fără migrări:
//   dotnet run -- import-gtfs --dry-run <folder | fișier.zip | URL>
if (args.Length >= 3 && args[0] == "import-gtfs" && args[1] == "--dry-run")
{
    Environment.ExitCode = await GtfsCli.DryRunAsync(args[2]);
    return;
}

// Antrenarea politicii RL de holding în digital twin (fără bază de date):
//   dotnet run -- train-rl [episoade] [candidați] [fișier ieșire]
if (args.Length >= 1 && args[0] == "train-rl")
{
    var episodes = args.Length >= 2 && int.TryParse(args[1], out var e) ? e : 3000;
    var candidates = args.Length >= 3 && int.TryParse(args[2], out var c) ? c : 5;
    var output = args.Length >= 4 ? args[3] : Path.Combine("Resources", "rl-hold-policy.json");
    Console.WriteLine($"🧠 Antrenez {candidates} candidați × {episodes} episoade și îl aleg pe cel mai bun pe validare...");
    var policy = RlTrainer.TrainSelected(candidates, episodes, log: Console.WriteLine);
    File.WriteAllText(output, policy.ToJson());
    Console.WriteLine($"✅ Politica RL scrisă în {output}");
    return;
}

// Experimentul de comparare a strategiilor de control pe seed-urile de test (rezultatele din docs/results):
//   dotnet run -- twin-experiment [folder ieșire] [replicări]
if (args.Length >= 1 && args[0] == "twin-experiment")
{
    var outDir = args.Length >= 2 ? args[1] : Path.Combine("..", "docs", "results");
    var reps = args.Length >= 3 && int.TryParse(args[2], out var r) ? r : 20;
    Directory.CreateDirectory(outDir);
    var report = TwinExperiment.Run(reps, Console.WriteLine);
    File.WriteAllText(Path.Combine(outDir, "bunching-control.json"), report.ToJson());
    File.WriteAllText(Path.Combine(outDir, "bunching-control.md"), report.ToMarkdown());
    Console.WriteLine($"✅ Rezultate scrise în {outDir}");
    return;
}

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

// Platforma live (event-driven): ingest -> stare în memorie + coadă de evenimente -> handler-e (SignalR, ...)
builder.Services.AddSignalR();
builder.Services.AddSingleton<LiveBusState>();
builder.Services.AddSingleton<LiveEventBus>();
builder.Services.AddSingleton<LiveIngestService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBusPositionHandler, HubBroadcastHandler>();

// Pipeline pe evenimente: Kafka (vehicle-positions) -> LiveIngestService; opțional istoric în PostgreSQL pentru replay
var kafkaOptions = builder.Configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();
kafkaOptions.BootstrapServers ??= Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
builder.Services.AddSingleton(kafkaOptions);
if (!string.IsNullOrWhiteSpace(kafkaOptions.BootstrapServers))
{
    builder.Services.AddSingleton<KafkaPositionConsumer>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<KafkaPositionConsumer>());
}

// Ingestia MQTT integrată (fără worker/Kafka): doar cu Ingestion:EmbeddedMqtt=true și Mqtt:Host setat
if (builder.Configuration.GetValue<bool>("Ingestion:EmbeddedMqtt") && !string.IsNullOrWhiteSpace(builder.Configuration["Mqtt:Host"]))
{
    var mqttOptions = builder.Configuration.GetSection("Mqtt").Get<TursibMessaging.MqttOptions>() ?? new TursibMessaging.MqttOptions();
    builder.Services.AddSingleton(mqttOptions);
    builder.Services.AddSingleton<EmbeddedMqttIngest>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<EmbeddedMqttIngest>());
}

var historyOptions = builder.Configuration.GetSection("History").Get<HistoryOptions>() ?? new HistoryOptions();
builder.Services.AddSingleton(historyOptions);
if (historyOptions.Enabled)
{
    builder.Services.AddSingleton<PositionHistoryBuffer>();
    builder.Services.AddSingleton<IBusPositionHandler, PositionHistoryHandler>();
    builder.Services.AddSingleton<PositionHistoryWriter>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<PositionHistoryWriter>());
}

// Stage 2: detecție bunching (pragurile se pot suprascrie din secțiunea „Bunching”)
builder.Services.AddSingleton(builder.Configuration.GetSection("Bunching").Get<BunchingOptions>() ?? new BunchingOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Holding").Get<HoldOptions>() ?? new HoldOptions());
builder.Services.AddSingleton<IRouteGeometryProvider, DbRouteGeometryProvider>();
builder.Services.AddSingleton<BunchingDetector>();

// trip_id pentru feed-ul GTFS-RT: vehiculele se potrivesc cu cursele programate azi
builder.Services.AddSingleton(builder.Configuration.GetSection("TripMatching").Get<TripMatchOptions>() ?? new TripMatchOptions());
builder.Services.AddSingleton<TripMatcher>();

// Stage 3: digital twin (simulare accelerată a liniilor, independentă de fluxul live)
builder.Services.AddSingleton<TwinService>();
builder.Services.AddSingleton<IBusPositionHandler>(sp => sp.GetRequiredService<BunchingDetector>());
builder.Services.AddHostedService<LiveEventDispatcher>();
builder.Services.AddHostedService<StaleBusCleaner>();

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
    //   dotnet run -- import-gtfs <folder GTFS | fișier.zip | URL>   (ex. https://tursib.ro/trasee/gtfs)
    if (args.Length >= 2 && args[0] is "copy-from-sqlite" or "import-gtfs")
    {
        if (args[0] == "copy-from-sqlite")
        {
            Console.WriteLine($"📦 Copying data from {args[1]} ...");
            await DatabaseMaintenance.CopyFromSqliteAsync(db, args[1]);
        }
        else
        {
            Environment.ExitCode = await GtfsCli.ImportAsync(db, args[1]);
            if (Environment.ExitCode != 0) return;
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
app.MapHub<TursibBackend.Hubs.BusHub>("/hubs/buses");

// Endpoint pentru import GTFS
app.MapPost("/api/import-gtfs", async (ApplicationDbContext db, IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        // GTFS_PATH poate fi un folder, un .zip sau un URL (ex. https://tursib.ro/trasee/gtfs)
        var gtfsPath = Environment.GetEnvironmentVariable("GTFS_PATH")
            ?? config["GtfsPath"]
            ?? throw new InvalidOperationException("GTFS path not configured");

        using var feed = await TursibBackend.GtfsFeedSource.ResolveAsync(gtfsPath);
        var missing = feed.MissingFiles();
        if (missing.Count > 0)
            return Results.BadRequest(new { message = $"Feed incomplet, lipsesc: {string.Join(", ", missing)}" });
        await new TursibBackend.GTFSImporter(db, feed.Folder).ImportAllAsync();
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
            serviceCalendars = await db.ServiceCalendars.CountAsync(),
            serviceCalendarDates = await db.ServiceCalendarDates.CountAsync()
        });
    })
    .WithName("DebugGTFS")
    .RequireAuthorization(policy => policy.RequireRole("Admin"));

    // Ce servicii GTFS circulă într-o zi (calendar + excepții), ex. /api/debug/services?date=2026-12-01
    app.MapGet("/api/debug/services", async (DateTime date, TursibBackend.Services.RouteCalculatorService planner) =>
        Results.Ok(new { date = date.ToString("yyyy-MM-dd"), services = (await planner.GetActiveServiceIdsAsync(date)).Order() }))
    .WithName("DebugServices");
}

app.Run();

public partial class Program { }

/// <summary>Comenzile CLI pentru feed-ul GTFS (verificare și import).</summary>
public static class GtfsCli
{
    public static async Task<int> DryRunAsync(string source)
    {
        try
        {
            using var feed = await TursibBackend.GtfsFeedSource.ResolveAsync(source);
            return Report(feed) ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ImportAsync(ApplicationDbContext db, string source)
    {
        try
        {
            using var feed = await TursibBackend.GtfsFeedSource.ResolveAsync(source);
            if (!Report(feed)) return 1;
            await new TursibBackend.GTFSImporter(db, feed.Folder).ImportAllAsync();
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
            return 1;
        }
    }

    /// <summary>Afișează conținutul feed-ului; false dacă lipsesc fișiere obligatorii.</summary>
    private static bool Report(TursibBackend.GtfsFeedSource feed)
    {
        foreach (var (file, rows) in feed.RowCounts()) Console.WriteLine($"   {file,-20} {rows,8} rânduri");
        if (!feed.RowCounts().ContainsKey("calendar.txt"))
            Console.WriteLine("   ⚠️ fără calendar.txt: planificatorul și potrivirea curselor nu vor avea orar pe zile");
        var missing = feed.MissingFiles();
        if (missing.Count == 0) return true;
        Console.Error.WriteLine($"❌ Feed incomplet, lipsesc: {string.Join(", ", missing)}");
        return false;
    }
}
