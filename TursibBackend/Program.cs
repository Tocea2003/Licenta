using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.ResponseCompression;
using System.Text;
using TursibBackend.Data;
using TursibBackend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Cache + Response Compression
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<RouteCalculatorService>();

// JWT Key: prioritate environment variable > appsettings
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT Key not configured. Set the JWT_KEY environment variable or Jwt:Key in appsettings.");

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:8080",
                  "http://localhost:5173",
                  "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseResponseCompression();
app.UseCors("AllowVueApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Endpoint pentru import GTFS
app.MapPost("/api/import-gtfs", () =>
{
    try
    {
        TursibBackend.RunGTFSImport.ExecuteImport();
        return Results.Ok(new { message = "GTFS import completed successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Import failed: {ex.Message}");
    }
})
.WithName("ImportGTFS");

// Endpoint DEBUG pentru RouteStations
app.MapGet("/api/debug/routestations", () =>
{
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=TursibDb.db");
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
.WithName("DebugRouteStations");

// Endpoint DEBUG pentru Trips și StopTimes
app.MapGet("/api/debug/gtfs", () =>
{
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=TursibDb.db");
    conn.Open();
    
    var info = new Dictionary<string, object>();
    
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Routes";
    info["routes"] = cmd.ExecuteScalar();
    
    cmd.CommandText = "SELECT COUNT(*) FROM Stations";
    info["stations"] = cmd.ExecuteScalar();
    
    cmd.CommandText = "SELECT COUNT(*) FROM Trips";
    info["trips"] = cmd.ExecuteScalar();
    
    cmd.CommandText = "SELECT COUNT(*) FROM StopTimes";
    info["stopTimes"] = cmd.ExecuteScalar();
    
    cmd.CommandText = "SELECT COUNT(*) FROM Shapes";
    info["shapes"] = cmd.ExecuteScalar();
    
    // Sample trip for route 1
    cmd.CommandText = "SELECT TripId FROM Trips WHERE RouteId = 1 LIMIT 1";
    var tripId = cmd.ExecuteScalar()?.ToString();
    info["sampleTripRoute1"] = tripId;
    
    if (!string.IsNullOrEmpty(tripId))
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM StopTimes WHERE TripId = '{tripId}'";
        info["stopsInSampleTrip"] = cmd.ExecuteScalar();
        
        // Get first stop details
        cmd.CommandText = $"SELECT StopId, StopSequence FROM StopTimes WHERE TripId = '{tripId}' ORDER BY StopSequence LIMIT 3";
        var stops = new List<object>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var stopId = reader.GetInt32(0);
                var seq = reader.GetInt32(1);
                
                // Check if this StopId exists in Stations
                var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"SELECT COUNT(*) FROM Stations WHERE Id = {stopId}";
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                
                stops.Add(new { stopId, sequence = seq, existsInStations = exists });
            }
        }
        info["sampleStops"] = stops;
    }
    
    return Results.Ok(info);
})
.WithName("DebugGTFS");

app.Run();
