using TursibBackend.Data;
using TursibBackend.Models;
using Microsoft.EntityFrameworkCore;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend.Services
{
    public class RouteCalculatorService
    {
        private readonly ApplicationDbContext _context;

        public RouteCalculatorService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Calculează cea mai bună rută între două stații
        public async Task<CalculatedRoute?> CalculateOptimalRoute(
            int startStationId, 
            int endStationId,
            DateTime? departureTime = null)
        {
            var routes = await CalculateAlternativeRoutes(startStationId, endStationId, departureTime);
            return routes.FirstOrDefault(); // Returnează prima rută (cea mai rapidă)
        }

        // Calculează 2-3 rute alternative între două stații
        public async Task<List<CalculatedRoute>> CalculateAlternativeRoutes(
            int startStationId, 
            int endStationId,
            DateTime? departureTime = null)
        {
            var departure = departureTime ?? DateTime.Now;
            var alternativeRoutes = new List<CalculatedRoute>();

            // Încarcă toate traseele și stațiile
            var routes = await _context.Routes
                .Include(r => r.RouteStations)
                .ThenInclude(rs => rs.Station)
                .ToListAsync();

            if (routes.Count == 0)
            {
                return alternativeRoutes;
            }

            // 1. Găsește toate rutele directe posibile
            var directRoutes = FindAllDirectRoutes(routes, startStationId, endStationId);
            alternativeRoutes.AddRange(directRoutes);

            // 2. Găsește toate rutele cu un transfer
            var transferRoutes = FindAllRoutesWithTransfer(routes, startStationId, endStationId);
            alternativeRoutes.AddRange(transferRoutes);

            // 3. Sortează și limitează rezultatele
            alternativeRoutes = alternativeRoutes
                .OrderBy(r => r.TotalDuration) // Sortează după timp
                .ThenBy(r => r.Segments.Count) // Apoi după număr de segmente
                .Take(3) // Păstrează doar top 3
                .ToList();

            // 4. Adaugă metadata pentru fiecare rută
            for (int i = 0; i < alternativeRoutes.Count; i++)
            {
                alternativeRoutes[i].RouteRank = i + 1;
                
                // Categorisire rută
                if (i == 0)
                    alternativeRoutes[i].RouteCategory = "Cea mai rapidă";
                else if (alternativeRoutes[i].Segments.Count < alternativeRoutes[0].Segments.Count)
                    alternativeRoutes[i].RouteCategory = "Mai puține transferuri";
                else
                    alternativeRoutes[i].RouteCategory = "Rută alternativă";
            }

            return alternativeRoutes;
        }

        // Găsește toate rutele directe posibile
        private List<CalculatedRoute> FindAllDirectRoutes(
            List<RouteModel> routes, 
            int startStationId, 
            int endStationId)
        {
            var directRoutes = new List<CalculatedRoute>();

            foreach (var route in routes)
            {
                var stations = route.RouteStations
                    .OrderBy(rs => rs.Order)
                    .ToList();

                var startIndex = stations.FindIndex(rs => rs.StationId == startStationId);
                var endIndex = stations.FindIndex(rs => rs.StationId == endStationId);

                if (startIndex >= 0 && endIndex >= 0 && startIndex < endIndex)
                {
                    // Găsit traseu direct
                    var stationsBetween = stations
                        .Skip(startIndex)
                        .Take(endIndex - startIndex + 1)
                        .Select(rs => rs.Station!)
                        .ToList();

                    // Estimare timp de călătorie (2 minute per stație)
                    var estimatedDuration = (endIndex - startIndex) * 2;

                    directRoutes.Add(new CalculatedRoute
                    {
                        RouteType = "direct",
                        TotalDuration = estimatedDuration,
                        Segments = new List<RouteSegment>
                        {
                            new RouteSegment
                            {
                                Type = "bus",
                                RouteNumber = route.RouteNumber,
                                RouteName = route.Name,
                                Color = route.Color,
                                StartStation = stationsBetween.First(),
                                EndStation = stationsBetween.Last(),
                                Duration = estimatedDuration,
                                StationCount = stationsBetween.Count
                            }
                        }
                    });
                }
            }

            return directRoutes;
        }

        // Găsește toate rutele cu un transfer
        private List<CalculatedRoute> FindAllRoutesWithTransfer(
            List<RouteModel> routes,
            int startStationId,
            int endStationId)
        {
            var transferRoutes = new List<CalculatedRoute>();

            // Caută toate combinațiile posibile cu un transfer
            foreach (var route1 in routes)
            {
                var stations1 = route1.RouteStations
                    .OrderBy(rs => rs.Order)
                    .ToList();

                var startIndex = stations1.FindIndex(rs => rs.StationId == startStationId);
                if (startIndex < 0) continue;

                // Pentru fiecare stație după start pe primul traseu
                for (int transferIndex = startIndex + 1; transferIndex < stations1.Count; transferIndex++)
                {
                    var transferStationId = stations1[transferIndex].StationId;

                    // Caută al doilea traseu care trece prin stația de transfer
                    foreach (var route2 in routes)
                    {
                        if (route2.Id == route1.Id) continue; // Skip same route

                        var stations2 = route2.RouteStations
                            .OrderBy(rs => rs.Order)
                            .ToList();

                        var transferIndex2 = stations2.FindIndex(rs => rs.StationId == transferStationId);
                        var endIndex2 = stations2.FindIndex(rs => rs.StationId == endStationId);

                        if (transferIndex2 >= 0 && endIndex2 >= 0 && transferIndex2 < endIndex2)
                        {
                            // Găsit traseu cu transfer
                            var segment1Stations = stations1
                                .Skip(startIndex)
                                .Take(transferIndex - startIndex + 1)
                                .Select(rs => rs.Station!)
                                .ToList();

                            var segment2Stations = stations2
                                .Skip(transferIndex2)
                                .Take(endIndex2 - transferIndex2 + 1)
                                .Select(rs => rs.Station!)
                                .ToList();

                            var duration1 = (transferIndex - startIndex) * 2;
                            var duration2 = (endIndex2 - transferIndex2) * 2;
                            var transferWaitTime = 5; // 5 minute așteptare

                            transferRoutes.Add(new CalculatedRoute
                            {
                                RouteType = "transfer",
                                TotalDuration = duration1 + duration2 + transferWaitTime,
                                Segments = new List<RouteSegment>
                                {
                                    new RouteSegment
                                    {
                                        Type = "bus",
                                        RouteNumber = route1.RouteNumber,
                                        RouteName = route1.Name,
                                        Color = route1.Color,
                                        StartStation = segment1Stations.First(),
                                        EndStation = segment1Stations.Last(),
                                        Duration = duration1,
                                        StationCount = segment1Stations.Count
                                    },
                                    new RouteSegment
                                    {
                                        Type = "transfer",
                                        Duration = transferWaitTime,
                                        StartStation = segment1Stations.Last(),
                                        EndStation = segment2Stations.First()
                                    },
                                    new RouteSegment
                                    {
                                        Type = "bus",
                                        RouteNumber = route2.RouteNumber,
                                        RouteName = route2.Name,
                                        Color = route2.Color,
                                        StartStation = segment2Stations.First(),
                                        EndStation = segment2Stations.Last(),
                                        Duration = duration2,
                                        StationCount = segment2Stations.Count
                                    }
                                }
                            });
                        }
                    }
                }
            }

            return transferRoutes;
        }

        // Calculează distanța Haversine între două coordonate
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Raza Pământului în km

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
    }

    // Response models
    public class CalculatedRoute
    {
        public string RouteType { get; set; } = "direct"; // "direct" sau "transfer"
        public int TotalDuration { get; set; } // în minute
        public List<RouteSegment> Segments { get; set; } = new();
        public int RouteRank { get; set; } // 1 = cea mai bună, 2 = a doua, etc.
        public string RouteCategory { get; set; } = ""; // "Cea mai rapidă", "Mai puține transferuri", etc.
    }

    public class RouteSegment
    {
        public string Type { get; set; } = "bus"; // "bus", "walk", "transfer"
        public string? RouteNumber { get; set; }
        public string? RouteName { get; set; }
        public string? Color { get; set; }
        public Station? StartStation { get; set; }
        public Station? EndStation { get; set; }
        public int Duration { get; set; } // în minute
        public int StationCount { get; set; }
    }
}
