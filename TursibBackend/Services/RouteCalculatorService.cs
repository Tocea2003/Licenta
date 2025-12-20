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
            var departure = departureTime ?? DateTime.Now;

            // Încarcă toate traseele și stațiile
            var routes = await _context.Routes
                .Include(r => r.RouteStations)
                .ThenInclude(rs => rs.Station)
                .ToListAsync();

            if (routes.Count == 0)
            {
                return null;
            }

            // Găsește trasee directe (fără transfer)
            var directRoute = FindDirectRoute(routes, startStationId, endStationId);
            if (directRoute != null)
            {
                return directRoute;
            }

            // Găsește trasee cu un transfer
            var transferRoute = FindRouteWithTransfer(routes, startStationId, endStationId);
            if (transferRoute != null)
            {
                return transferRoute;
            }

            // Dacă nu se găsește nimic, returnează null
            return null;
        }

        private CalculatedRoute? FindDirectRoute(
            List<RouteModel> routes, 
            int startStationId, 
            int endStationId)
        {
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

                    return new CalculatedRoute
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
                    };
                }
            }

            return null;
        }

        private CalculatedRoute? FindRouteWithTransfer(
            List<RouteModel> routes,
            int startStationId,
            int endStationId)
        {
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

                            return new CalculatedRoute
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
                            };
                        }
                    }
                }
            }

            return null;
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
