using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TursibBackend.Data;
using TursibBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configurare DbContext pentru Entity Framework Core cu SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register JWT Service
builder.Services.AddScoped<JwtService>();

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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")))
        };
    });

builder.Services.AddAuthorization();

// Configurare CORS pentru a permite cereri de la frontend (Vue.js)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:8080", 
                  "http://localhost:5173",
                  "http://localhost:5174") // Pentru AdminApp
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Adaugă suport pentru controllere
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Activează politica CORS
app.UseCors("AllowVueApp");

// app.UseHttpsRedirection(); // Dezactivat pentru development - HTTP only

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Mapează controllerele
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

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
