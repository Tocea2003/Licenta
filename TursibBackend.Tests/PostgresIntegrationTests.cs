using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using Xunit;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend.Tests;

/// <summary>
/// Runs only when TEST_POSTGRES_CONNECTION is set (CI provides a Postgres service).
/// Verifies the migrations apply and the LINQ queries translate to real PostgreSQL SQL,
/// which the InMemory provider cannot check.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")))
            Skip = "Set TEST_POSTGRES_CONNECTION to run PostgreSQL integration tests";
    }
}

public class PostgresIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"tursib_test_{Guid.NewGuid():N}";
    private string _adminConnection = "";
    private ApplicationDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrEmpty(baseConnection))
            return;

        _adminConnection = baseConnection;
        var testConnection = new NpgsqlConnectionStringBuilder(baseConnection) { Database = _databaseName }.ConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testConnection)
            .Options;
        _db = new ApplicationDbContext(options);
        await _db.Database.MigrateAsync(); // creates the database and applies all migrations
    }

    public async Task DisposeAsync()
    {
        if (_db == null)
            return;

        await _db.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_adminConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task SchedulePlanner_WorksOnPostgres()
    {
        var stations = new[]
        {
            new Station { Id = 100, Name = "A", Latitude = 45.76, Longitude = 24.13 },
            new Station { Id = 101, Name = "B", Latitude = 45.77, Longitude = 24.13 },
            new Station { Id = 102, Name = "C", Latitude = 45.78, Longitude = 24.13 }
        };
        _db.Routes.Add(new RouteModel { Id = 1, RouteNumber = "1", Name = "Route 1" });
        _db.Stations.AddRange(stations);
        for (int i = 0; i < stations.Length; i++)
            _db.RouteStations.Add(new RouteStation { RouteId = 1, StationId = stations[i].Id, Order = i + 1 });

        _db.ServiceCalendars.Add(new ServiceCalendar
        {
            ServiceId = "WEEKDAY",
            Monday = true, Tuesday = true, Wednesday = true, Thursday = true, Friday = true,
            StartDate = "2026-01-01",
            EndDate = "2026-12-31"
        });
        _db.Trips.Add(new Trip { TripId = "T0830", RouteId = 1, ServiceId = "WEEKDAY", DirectionId = 0 });
        for (int i = 0; i < stations.Length; i++)
        {
            var time = new TimeSpan(8, 30 + i * 5, 0).ToString(@"hh\:mm\:ss");
            _db.StopTimes.Add(new StopTime
            {
                TripId = "T0830", StopId = stations[i].Id, StopSequence = i + 1,
                ArrivalTime = time, DepartureTime = time
            });
        }
        await _db.SaveChangesAsync();

        var sut = new RouteCalculatorService(_db, new MemoryCache(new MemoryCacheOptions()), NullLogger<RouteCalculatorService>.Instance);
        var routes = await sut.CalculateAlternativeRoutes(100, 102, departureTime: new DateTime(2026, 4, 27, 8, 10, 0));

        Assert.NotEmpty(routes);
        Assert.Equal(new DateTime(2026, 4, 27, 8, 40, 0), routes[0].ArrivalTime);
    }

    [PostgresFact]
    public async Task ResetIdentitySequences_AllowsInsertAfterExplicitIds()
    {
        // GTFS import / SQLite copy insert explicit ids; the sequence must be moved past them
        _db.Stations.Add(new Station { Id = 5000, Name = "Imported", Latitude = 45.8, Longitude = 24.1 });
        await _db.SaveChangesAsync();

        await DatabaseMaintenance.ResetIdentitySequencesAsync(_db);

        var created = new Station { Name = "Created by admin", Latitude = 45.8, Longitude = 24.1 };
        _db.Stations.Add(created);
        await _db.SaveChangesAsync();

        Assert.True(created.Id > 5000);
    }
}
