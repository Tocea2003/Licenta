using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;

namespace BusSimulator
{
    class Program
    {
        public static readonly string FirebaseUrl = Environment.GetEnvironmentVariable("FIREBASE_URL")
            ?? "https://licenta-ulbs-default-rtdb.europe-west1.firebasedatabase.app/";
        public static readonly string ApiUrl = Environment.GetEnvironmentVariable("TURSIB_API_URL")
            ?? "http://localhost:5022/api";
        private const int BusesPerRoute = 3;

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚌 Tursib Bus Simulator - Starting...");
            Console.WriteLine("========================================");

            var firebaseClient = new FirebaseClient(FirebaseUrl);
            var httpClient = new HttpClient();

            var routes = await LoadAllRoutes(httpClient);

            if (routes.Count == 0)
            {
                Console.WriteLine("❌ No routes found!");
                return;
            }

            Console.WriteLine($"📍 Found {routes.Count} routes in GTFS");

            // Initialize all buses
            var allBuses = new List<BusSimulator>();
            var busId = 1;

            foreach (var route in routes)
            {
                for (int i = 0; i < BusesPerRoute; i++)
                {
                    var sim = new BusSimulator(busId++, route, i, BusesPerRoute, firebaseClient, httpClient);
                    allBuses.Add(sim);
                }
            }

            // Load routes for all buses in parallel
            Console.WriteLine($"⏳ Initializing {allBuses.Count} buses...");
            await Task.WhenAll(allBuses.Select(b => b.Initialize()));

            var readyBuses = allBuses.Where(b => b.IsReady).ToList();
            Console.WriteLine($"✅ {readyBuses.Count} buses ready!");
            Console.WriteLine("📡 Moving all buses simultaneously every 1 second...");
            Console.WriteLine("Press Ctrl+C to stop.\n");

            // Central loop: move ALL buses at once, then send ALL updates
            int tick = 0;
            while (true)
            {
                var updates = new List<Task>();
                foreach (var bus in readyBuses)
                {
                    bus.Tick();
                    updates.Add(bus.SendUpdate());
                }
                await Task.WhenAll(updates);

                tick++;
                if (tick % 30 == 0)
                    Console.WriteLine($"📡 Tick {tick}: {readyBuses.Count} buses updated");

                await Task.Delay(500);
            }
        }

        static async Task<List<RouteInfo>> LoadAllRoutes(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"{ApiUrl}/routes");
                return JsonConvert.DeserializeObject<List<RouteInfo>>(response) ?? new List<RouteInfo>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load routes: {ex.Message}");
                return new List<RouteInfo>();
            }
        }
    }

    class BusSimulator
    {
        private readonly int busId;
        private readonly RouteInfo route;
        private readonly int busIndex;
        private readonly int totalBuses;
        private readonly FirebaseClient firebase;
        private readonly HttpClient httpClient;
        private List<Station> stations = new List<Station>();
        private List<(double Latitude, double Longitude)> routePoints = new List<(double, double)>();
        private int currentPointIndex = 0;
        private bool isReturning = false;
        private int occupancy = 0;
        private string status = "in_transit";
        private Random random = new Random();
        private bool isDwelling = false;
        private DateTime dwellEndTime = DateTime.MinValue;
        private double lastSpeed = 30;

        public bool IsReady => routePoints.Count > 0;

        public BusSimulator(int busId, RouteInfo route, int busIndex, int totalBuses,
            FirebaseClient firebase, HttpClient httpClient)
        {
            this.busId = busId;
            this.route = route;
            this.busIndex = busIndex;
            this.totalBuses = totalBuses;
            this.firebase = firebase;
            this.httpClient = httpClient;
        }

        public async Task Initialize()
        {
            await LoadStations();
            if (stations.Count == 0) return;

            await CalculateRoutePoints();
            if (routePoints.Count == 0) return;

            currentPointIndex = (busIndex * routePoints.Count) / totalBuses;
            Console.WriteLine($"✅ Bus {busId} (R{route.RouteNumber} #{busIndex+1}): {routePoints.Count} pts, start at {currentPointIndex}");
        }

        public void Tick()
        {
            if (isDwelling && DateTime.Now < dwellEndTime)
            {
                status = "at_station";
                return;
            }

            if (isDwelling)
            {
                isDwelling = false;
                status = "departing";
            }

            var location = GetCurrentLocation();
            var nearestStation = GetNearestStation(location);
            var distToNearest = nearestStation != null
                ? HaversineMeters(location.Latitude, location.Longitude, nearestStation.Latitude, nearestStation.Longitude)
                : double.MaxValue;

            if (distToNearest < 15)
            {
                UpdateOccupancy(nearestStation);
                isDwelling = true;
                dwellEndTime = DateTime.Now.AddMilliseconds(CalculateDwellTime());
                status = "at_station";
            }
            else if (distToNearest < 80)
            {
                status = "approaching";
            }
            else
            {
                status = "in_transit";
            }

            lastSpeed = CalculateRealisticSpeed(distToNearest);
            MoveToNextPoint();
        }

        public async Task SendUpdate()
        {
            var location = GetCurrentLocation();
            var nextStation = GetNextStation();
            var etaSeconds = nextStation != null ? CalculateEtaSeconds(nextStation, lastSpeed) : 0;
            await SendLocationWithRetry(location, lastSpeed, nextStation, etaSeconds);
        }

        private async Task SendLocationWithRetry(
            (double Latitude, double Longitude) location,
            double speed = 0,
            Station? nextStation = null,
            int etaSeconds = 0,
            int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var payload = new Dictionary<string, object>
                    {
                        ["latitude"] = location.Latitude,
                        ["longitude"] = location.Longitude,
                        ["routeId"] = route.Id,
                        ["routeNumber"] = route.RouteNumber,
                        ["routeName"] = route.Name,
                        ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        ["speed"] = Math.Round(speed, 1),
                        ["heading"] = Math.Round(CalculateHeading(), 1),
                        ["occupancy"] = occupancy,
                        ["status"] = status,
                        ["directionId"] = isReturning ? 1 : 0,
                        ["busIndex"] = busIndex
                    };

                    if (nextStation != null)
                    {
                        payload["nextStationId"] = nextStation.Id;
                        payload["nextStationName"] = nextStation.Name;
                        payload["nextStationEta"] = etaSeconds;
                    }

                    await firebase
                        .Child("bus_locations")
                        .Child($"route_{route.Id}_bus_{busIndex}")
                        .PutAsync(payload);

                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"❌ Bus {busId} Firebase error: {ex.Message}");
                    }
                    else
                    {
                        await Task.Delay(500 * attempt);
                    }
                }
            }
        }

        private double CalculateRealisticSpeed(double distToNearestStation)
        {
            double baseSpeed;

            if (distToNearestStation < 30)
                baseSpeed = 5 + random.NextDouble() * 10;
            else if (distToNearestStation < 100)
                baseSpeed = 15 + random.NextDouble() * 10;
            else if (distToNearestStation < 200)
                baseSpeed = 25 + random.NextDouble() * 10;
            else
                baseSpeed = 35 + random.NextDouble() * 15;

            var hour = DateTime.Now.Hour;
            if ((hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 18))
                baseSpeed *= 0.75;
            else if (hour >= 22 || hour <= 5)
                baseSpeed *= 1.1;

            return Math.Max(3, baseSpeed);
        }

        private int CalculateDwellTime()
        {
            int baseDwell = random.Next(10000, 30000);
            var hour = DateTime.Now.Hour;

            if ((hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 18))
                baseDwell += random.Next(5000, 15000);
            else if (hour >= 22 || hour <= 5)
                baseDwell = Math.Max(5000, baseDwell - 10000);

            return baseDwell;
        }

        private Station? GetNearestStation((double Latitude, double Longitude) location)
        {
            Station? nearest = null;
            double minDist = double.MaxValue;

            foreach (var s in stations)
            {
                double dist = HaversineMeters(location.Latitude, location.Longitude, s.Latitude, s.Longitude);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = s;
                }
            }

            return nearest;
        }

        private Station? GetNextStation()
        {
            if (stations.Count == 0 || routePoints.Count == 0)
                return null;

            var current = GetCurrentLocation();
            int direction = isReturning ? -1 : 1;
            int searchIdx = currentPointIndex;

            for (int step = 0; step < routePoints.Count; step++)
            {
                searchIdx += direction;
                if (searchIdx >= routePoints.Count) searchIdx = routePoints.Count - 1;
                if (searchIdx < 0) searchIdx = 0;

                var point = routePoints[searchIdx];
                foreach (var station in stations)
                {
                    double dist = HaversineMeters(point.Latitude, point.Longitude, station.Latitude, station.Longitude);
                    if (dist < 30)
                    {
                        double currentDist = HaversineMeters(current.Latitude, current.Longitude, station.Latitude, station.Longitude);
                        if (currentDist > 50)
                            return station;
                    }
                }
            }

            return stations.LastOrDefault();
        }

        private int CalculateEtaSeconds(Station nextStation, double currentSpeed)
        {
            var location = GetCurrentLocation();
            double distKm = HaversineMeters(location.Latitude, location.Longitude,
                nextStation.Latitude, nextStation.Longitude) / 1000.0;

            double avgSpeed = Math.Max(10, currentSpeed * 0.7);
            double hours = distKm / avgSpeed;

            return (int)(hours * 3600);
        }

        private async Task LoadStations()
        {
            try
            {
                var response = await httpClient.GetStringAsync($"{Program.ApiUrl}/routes/{route.Id}/stations");
                stations = JsonConvert.DeserializeObject<List<Station>>(response) ?? new List<Station>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load stations for route {route.Id}: {ex.Message}");
            }
        }

        private async Task CalculateRoutePoints()
        {
            try
            {
                Console.WriteLine($"🗺️  Bus {busId}: Loading GTFS shape for route {route.Id}...");

                var shapeResponse = await httpClient.GetStringAsync($"{Program.ApiUrl}/shapes/route/{route.Id}");
                var shapeData = JsonConvert.DeserializeObject<dynamic>(shapeResponse);

                if (shapeData?.points != null && ((int)shapeData.points.Count) > 0)
                {
                    foreach (var point in shapeData!.points)
                    {
                        double lat = point.latitude;
                        double lon = point.longitude;
                        routePoints.Add((lat, lon));
                    }

                    Console.WriteLine($"   ✅ Loaded {routePoints.Count} GTFS shape points");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Bus {busId}: GTFS shapes not available ({ex.Message}), trying OSRM...");
            }

            try
            {
                var coordinates = string.Join(";", stations.Select(s => $"{s.Longitude},{s.Latitude}"));
                var url = $"https://router.project-osrm.org/route/v1/driving/{coordinates}" +
                          $"?overview=full&geometries=geojson";

                var response = await httpClient.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);

                if (data != null && data?.code == "Ok" && data?.routes != null && ((int)data!.routes.Count) > 0)
                {
                    dynamic geometry = data.routes[0].geometry.coordinates;

                    foreach (var coord in geometry)
                    {
                        double lon = coord[0];
                        double lat = coord[1];
                        routePoints.Add((lat, lon));
                    }

                    Console.WriteLine($"   ✅ Loaded {routePoints.Count} OSRM route points");
                }
                else
                {
                    Console.WriteLine($"⚠️ Bus {busId}: OSRM failed, using straight lines");
                    routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Bus {busId}: OSRM error: {ex.Message}");
                routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
            }
        }

        private (double Latitude, double Longitude) GetCurrentLocation()
        {
            if (routePoints.Count == 0)
                return (45.7970, 24.1523);

            return routePoints[currentPointIndex];
        }

        private void MoveToNextPoint()
        {
            // Move ~25 meters per tick by measuring actual GPS distance
            const double targetMeters = 25.0;
            double moved = 0;
            int dir = isReturning ? -1 : 1;

            while (moved < targetMeters)
            {
                int next = currentPointIndex + dir;
                if (next <= 0) { currentPointIndex = 0; isReturning = false; return; }
                if (next >= routePoints.Count - 1) { currentPointIndex = routePoints.Count - 1; isReturning = true; return; }

                moved += HaversineMeters(
                    routePoints[currentPointIndex].Latitude, routePoints[currentPointIndex].Longitude,
                    routePoints[next].Latitude, routePoints[next].Longitude);
                currentPointIndex = next;
            }

            // Keep the old boundary checks just in case
            if (isReturning)
            {
                if (currentPointIndex <= 0)
                {
                    currentPointIndex = 0;
                    isReturning = false;
                }
            }
            else
            {
                if (currentPointIndex >= routePoints.Count - 1)
                {
                    currentPointIndex = routePoints.Count - 1;
                    isReturning = true;
                }
            }
        }

        private double CalculateHeading()
        {
            if (routePoints.Count < 2)
                return 0;

            int nextIdx;
            if (isReturning)
                nextIdx = Math.Max(0, currentPointIndex - 1);
            else
                nextIdx = Math.Min(routePoints.Count - 1, currentPointIndex + 1);

            var current = routePoints[currentPointIndex];
            var next = routePoints[nextIdx];

            var dLon = next.Longitude - current.Longitude;
            var dLat = next.Latitude - current.Latitude;

            var heading = Math.Atan2(dLon, dLat) * 180 / Math.PI;
            return (heading + 360) % 360;
        }

        private void UpdateOccupancy(Station? station)
        {
            bool isTerminal = station != null &&
                (station == stations.FirstOrDefault() || station == stations.LastOrDefault());

            if (isTerminal)
            {
                occupancy = random.Next(0, 10);
                return;
            }

            int change = random.Next(-15, 25);
            occupancy = Math.Max(0, Math.Min(100, occupancy + change));

            var hour = DateTime.Now.Hour;
            bool isPeak = (hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 18);
            bool isNight = hour >= 22 || hour <= 5;
            bool isWeekend = DateTime.Now.DayOfWeek == DayOfWeek.Saturday ||
                             DateTime.Now.DayOfWeek == DayOfWeek.Sunday;

            if (isPeak && !isWeekend)
            {
                if (!isReturning && occupancy < 70)
                    occupancy += random.Next(5, 20);
                else if (isReturning && occupancy < 50)
                    occupancy += random.Next(3, 10);
            }
            else if (isNight)
            {
                if (occupancy > 20)
                    occupancy -= random.Next(5, 15);
            }

            if (isWeekend)
                occupancy = (int)(occupancy * 0.7);

            occupancy = Math.Max(0, Math.Min(100, occupancy));
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }

    class Station
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }

    class RouteInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("routeNumber")]
        public string RouteNumber { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("color")]
        public string Color { get; set; } = string.Empty;
    }
}
