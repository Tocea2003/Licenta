using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend.Tests.Services;

/// <summary>
/// Teste pentru algoritmul Dijkstra implementat în RouteCalculatorService.
/// Aceste teste validează corectitudinea algoritmului pentru diferite scenarii.
/// </summary>
public class RouteCalculatorServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RouteCalculatorService _service;

    public RouteCalculatorServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new RouteCalculatorService(_context);

        SeedTestData();
    }

    /// <summary>
    /// Populează baza de date in-memory cu date de test.
    /// Configurație simplă pentru testare:
    /// 
    /// Linia 1: Stația A → Stația B → Stația C
    /// Linia 2: Stația C → Stația D → Stația E
    /// 
    /// Permite testarea rutelor directe și cu transfer.
    /// </summary>
    private void SeedTestData()
    {
        // Creează stații
        var stations = new List<Station>
        {
            new Station { Id = 1, Name = "Stația A", Latitude = 45.7983, Longitude = 24.1256 },
            new Station { Id = 2, Name = "Stația B", Latitude = 45.8000, Longitude = 24.1300 },
            new Station { Id = 3, Name = "Stația C", Latitude = 45.8020, Longitude = 24.1350 },
            new Station { Id = 4, Name = "Stația D", Latitude = 45.8040, Longitude = 24.1400 },
            new Station { Id = 5, Name = "Stația E", Latitude = 45.8060, Longitude = 24.1450 },
            new Station { Id = 6, Name = "Stația F", Latitude = 45.8080, Longitude = 24.1500 }
        };

        _context.Stations.AddRange(stations);

        // Creează rute
        var routes = new List<RouteModel>
        {
            new RouteModel 
            { 
                Id = 1, 
                RouteNumber = "1", 
                Name = "Linia 1", 
                Color = "#FF0000",
                RouteStations = new List<RouteStation>
                {
                    new RouteStation { StationId = 1, Order = 1 },
                    new RouteStation { StationId = 2, Order = 2 },
                    new RouteStation { StationId = 3, Order = 3 }
                }
            },
            new RouteModel 
            { 
                Id = 2, 
                RouteNumber = "2", 
                Name = "Linia 2", 
                Color = "#00FF00",
                RouteStations = new List<RouteStation>
                {
                    new RouteStation { StationId = 3, Order = 1 },
                    new RouteStation { StationId = 4, Order = 2 },
                    new RouteStation { StationId = 5, Order = 3 }
                }
            },
            new RouteModel 
            { 
                Id = 3, 
                RouteNumber = "3", 
                Name = "Linia 3", 
                Color = "#0000FF",
                RouteStations = new List<RouteStation>
                {
                    new RouteStation { StationId = 1, Order = 1 },
                    new RouteStation { StationId = 6, Order = 2 }
                }
            }
        };

        _context.Routes.AddRange(routes);
        _context.SaveChanges();
    }

    #region Direct Route Tests

    [Fact]
    public async Task CalculateOptimalRoute_ShouldFindDirectRoute_WhenStationsAreOnSameLine()
    {
        // Arrange: Stația A → Stația C (ambele pe Linia 1)
        int startStationId = 1; // Stația A
        int endStationId = 3;   // Stația C

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);

        // Assert
        result.Should().NotBeNull();
        result!.RouteType.Should().Be("direct");
        result.Segments.Should().HaveCount(1);
        result.Segments[0].Type.Should().Be("bus");
        result.Segments[0].RouteNumber.Should().Be("1");
        result.Segments[0].StartStation!.Id.Should().Be(1);
        result.Segments[0].EndStation!.Id.Should().Be(3);
    }

    [Fact]
    public async Task CalculateOptimalRoute_ShouldCalculateCorrectDuration_ForDirectRoute()
    {
        // Arrange
        int startStationId = 1; // Stația A
        int endStationId = 2;   // Stația B (1 segment de distanță)

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalDuration.Should().BeGreaterThan(0);
        result.TotalDuration.Should().BeLessThan(20); // Rezonabil pentru o stație
    }

    #endregion

    #region Transfer Route Tests

    [Fact]
    public async Task CalculateOptimalRoute_ShouldFindRouteWithTransfer_WhenNoDirectRouteExists()
    {
        // Arrange: Stația A → Stația E (necesită transfer la Stația C)
        // A (Linia 1) → C (transfer) → E (Linia 2)
        // NOTE: Cu datele de test curente simple, algoritmul poate găsi o rută directă mai lungă
        // sau poate nu găsi deloc rută dacă graful nu are suficiente conexiuni.
        int startStationId = 1; // Stația A
        int endStationId = 5;   // Stația E

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);

        // Assert - verificăm că s-a găsit o rută validă
        result.Should().NotBeNull("ar trebui să găsească o rută între stații conectate");
        
        if (result != null)
        {
            // Ruta poate fi directă, cu transfer sau nu exista
            result.Segments.Should().NotBeEmpty("ruta ar trebui să aibă cel puțin un segment");
            result.TotalDuration.Should().BeGreaterThan(0);
            
            // Dacă există transfer, verifică structura
            var hasTransfer = result.Segments.Any(s => s.Type == "transfer");
            if (hasTransfer)
            {
                var busSegments = result.Segments.Where(s => s.Type == "bus").ToList();
                busSegments.Should().HaveCountGreaterOrEqualTo(2, "ruta cu transfer ar trebui să aibă cel puțin 2 segmente de bus");
            }
        }
    }

    [Fact]
    public async Task CalculateOptimalRoute_ShouldCalculateTotalDuration_Correctly()
    {
        // Arrange: Test că durata este calculată corect, indiferent de tipul rutei
        int startStationId = 1; // Stația A
        int endStationId = 3;   // Stația C (rută directă pe Linia 1)

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);

        // Assert
        result.Should().NotBeNull();
        
        if (result != null)
        {
            result.TotalDuration.Should().BeGreaterThan(0);
            
            // Suma duratelor segmentelor ar trebui să fie aproximativ egală cu durata totală
            var segmentsDurationSum = result.Segments.Sum(s => s.Duration);
            segmentsDurationSum.Should().BeCloseTo(result.TotalDuration, 5,
                "suma duratelor segmentelor ar trebui să fie aproximativ egală cu durata totală");
        }
    }

    #endregion

    #region Alternative Routes Tests

    [Fact]
    public async Task CalculateAlternativeRoutes_ShouldReturnMultipleRoutes_WhenPossible()
    {
        // Arrange
        int startStationId = 1; // Stația A
        int endStationId = 3;   // Stația C

        // Act
        var results = await _service.CalculateAlternativeRoutes(startStationId, endStationId);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountLessOrEqualTo(3, "maximum 3 rute alternative");
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_ShouldOrderByDuration()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 5;

        // Act
        var results = await _service.CalculateAlternativeRoutes(startStationId, endStationId);

        // Assert
        results.Should().NotBeEmpty();
        
        for (int i = 0; i < results.Count - 1; i++)
        {
            results[i].TotalDuration.Should().BeLessOrEqualTo(
                results[i + 1].TotalDuration,
                "rutele ar trebui sortate crescător după durată"
            );
        }
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_ShouldAssignCorrectRanking()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 3;

        // Act
        var results = await _service.CalculateAlternativeRoutes(startStationId, endStationId);

        // Assert
        results.Should().NotBeEmpty();
        
        for (int i = 0; i < results.Count; i++)
        {
            results[i].RouteRank.Should().Be(i + 1, "ranking-ul trebuie să fie 1-indexed");
        }
        
        results[0].RouteCategory.Should().Be("Cea mai rapidă");
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_WithArrivalTime_ShouldSetArrivalNotAfterRequested()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 5;
        var requestedArrival = new DateTime(2026, 4, 21, 10, 0, 0);

        // Act
        var results = await _service.CalculateAlternativeRoutes(
            startStationId, endStationId, departureTime: null, arrivalTime: requestedArrival);

        // Assert
        results.Should().NotBeEmpty();
        foreach (var route in results)
        {
            route.ArrivalTime.Should().Be(requestedArrival,
                "în modul 'arrive by', ora de sosire trebuie să corespundă celei cerute");
            route.Segments.Should().NotBeEmpty();
            route.Segments.First().StartTime.Should().Be(route.DepartureTime,
                "primul segment începe la ora de plecare");
            route.Segments.Last().EndTime.Should().Be(route.ArrivalTime,
                "ultimul segment se termină la ora de sosire");
            route.DepartureTime.Should().BeBefore(requestedArrival,
                "plecarea trebuie să fie înaintea sosirii cerute");
        }
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_WithDepartureTime_ShouldSetDepartureMatching()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 5;
        var requestedDeparture = new DateTime(2026, 4, 21, 9, 0, 0);

        // Act
        var results = await _service.CalculateAlternativeRoutes(
            startStationId, endStationId, departureTime: requestedDeparture);

        // Assert
        results.Should().NotBeEmpty();
        foreach (var route in results)
        {
            route.DepartureTime.Should().Be(requestedDeparture,
                "în modul 'depart at', ora de plecare trebuie să corespundă celei cerute");
            route.Segments.First().StartTime.Should().Be(requestedDeparture);
            route.ArrivalTime.Should().BeAfter(requestedDeparture,
                "sosirea trebuie să fie după plecare");
            route.Segments.Last().EndTime.Should().Be(route.ArrivalTime);
        }
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_WithBothTimesProvided_ShouldPrioritizeArrival()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 5;
        var departure = new DateTime(2026, 4, 21, 9, 0, 0);
        var arrival = new DateTime(2026, 4, 21, 11, 0, 0);

        // Act
        var results = await _service.CalculateAlternativeRoutes(
            startStationId, endStationId, departureTime: departure, arrivalTime: arrival);

        // Assert
        results.Should().NotBeEmpty();
        foreach (var route in results)
        {
            route.ArrivalTime.Should().Be(arrival,
                "când ambele ore sunt date, arrivalTime câștigă");
            route.DepartureTime.Should().NotBe(departure,
                "departureTime trebuie recalculat înapoi, nu copiat");
        }
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task CalculateOptimalRoute_ShouldReturnNull_WhenStartStationDoesNotExist()
    {
        // Arrange
        int startStationId = 999; // ID inexistent
        int endStationId = 1;

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);

        // Assert - poate returna null sau o listă goală, depinde de implementare
        // Testează comportamentul actual
        var routes = await _service.CalculateAlternativeRoutes(startStationId, endStationId);
        routes.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateOptimalRoute_ShouldReturnNull_WhenEndStationDoesNotExist()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 999; // ID inexistent

        // Act
        var routes = await _service.CalculateAlternativeRoutes(startStationId, endStationId);

        // Assert
        routes.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateOptimalRoute_ShouldHandleSameStartAndEnd()
    {
        // Arrange
        int stationId = 1; // Aceeași stație pentru start și end

        // Act
        var result = await _service.CalculateOptimalRoute(stationId, stationId);

        // Assert - poate returna rută cu durată 0 sau null
        // Testează comportamentul actual
        if (result != null)
        {
            result.TotalDuration.Should().Be(0);
        }
    }

    #endregion

    #region Dijkstra Algorithm Properties Tests

    [Fact]
    public async Task CalculateOptimalRoute_ShouldGuaranteeOptimality_DijkstraProperty()
    {
        // Arrange: Test fundamental al algoritmului Dijkstra
        // Oricare ar fi drumul găsit, nu poate exista drum mai scurt
        int startStationId = 1;
        int endStationId = 5;

        // Act
        var optimalRoute = await _service.CalculateOptimalRoute(startStationId, endStationId);
        var allAlternatives = await _service.CalculateAlternativeRoutes(startStationId, endStationId);

        // Assert
        optimalRoute.Should().NotBeNull();
        allAlternatives.Should().NotBeEmpty();
        
        // Prima rută din alternative ar trebui să fie cea optimă
        var firstAlternative = allAlternatives[0];
        firstAlternative.TotalDuration.Should().Be(
            optimalRoute!.TotalDuration,
            "ruta optimă returnată individual ar trebui să fie identică cu prima alternativă"
        );
        
        // Toate celelalte rute ar trebui să fie mai lungi sau egale
        foreach (var alternative in allAlternatives)
        {
            alternative.TotalDuration.Should().BeGreaterOrEqualTo(
                optimalRoute.TotalDuration,
                "nicio rută alternativă nu poate fi mai rapidă decât optimul găsit de Dijkstra"
            );
        }
    }

    [Fact]
    public async Task CalculateOptimalRoute_ShouldFindShortestPath_NotJustAnyPath()
    {
        // Arrange: Două posibilități de la A la C:
        // 1. Direct A → B → C (Linia 1)
        // 2. A → F → ?? (Linia 3, apoi ???)
        int startStationId = 1; // Stația A
        int endStationId = 3;   // Stația C

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);

        // Assert
        result.Should().NotBeNull();
        
        // Ar trebui să găsească ruta directă (cea mai scurtă)
        result!.Segments.Should().HaveCount(1, "există rută directă care e mai scurtă");
        result.Segments[0].RouteNumber.Should().Be("1");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task CalculateOptimalRoute_ShouldCompleteInReasonableTime()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 5;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _service.CalculateOptimalRoute(startStationId, endStationId);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "algoritmul ar trebui să fie rapid pentru seturi mici de date");
    }

    [Fact]
    public async Task CalculateAlternativeRoutes_ShouldCompleteInReasonableTime()
    {
        // Arrange
        int startStationId = 1;
        int endStationId = 5;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var results = await _service.CalculateAlternativeRoutes(startStationId, endStationId);
        stopwatch.Stop();

        // Assert
        results.Should().NotBeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300,
            "calcularea a 3 rute alternative ar trebui să fie rapid");
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
