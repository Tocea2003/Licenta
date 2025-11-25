using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;

namespace BusSimulator
{
    class Program
    {
        // URL-ul Firebase Realtime Database
        private const string FirebaseUrl = "https://licenta-ulbs-default-rtdb.europe-west1.firebasedatabase.app/";
        
        // URL-ul API-ului .NET
        public const string ApiUrl = "http://localhost:5022/api";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚌 Tursib Bus Simulator - Starting...");
            Console.WriteLine("========================================");
            
            var firebaseClient = new FirebaseClient(FirebaseUrl);
            var httpClient = new HttpClient();
            
            // Încarcă toate traseele din API
            var routes = await LoadAllRoutes(httpClient);
            
            if (routes.Count == 0)
            {
                Console.WriteLine("❌ No routes found!");
                return;
            }
            
            Console.WriteLine($"📍 Found {routes.Count} routes in GTFS");
            
            // Creează simulatoare pentru toate traseele
            var simulators = new List<Task>();
            var busId = 1;
            
            foreach (var route in routes)
            {
                var simulator = new BusSimulator(busId++, route.Id, firebaseClient, httpClient);
                simulators.Add(simulator.StartSimulation());
                
                // Delay mic între porniri pentru a nu supraîncărca API-ul
                await Task.Delay(200);
            }
            
            Console.WriteLine($"✅ {simulators.Count} simulators initialized!");
            Console.WriteLine("📡 Sending location updates...");
            Console.WriteLine("Press Ctrl+C to stop.\n");
            
            // Rulează toate simulatoarele în paralel
            await Task.WhenAll(simulators);
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
        private readonly int routeId;
        private readonly FirebaseClient firebase;
        private readonly HttpClient httpClient;
        private List<Station> stations = new List<Station>();
        private List<(double Latitude, double Longitude)> routePoints = new List<(double, double)>();
        private int currentPointIndex = 0;
        
        public BusSimulator(int busId, int routeId, FirebaseClient firebase, HttpClient httpClient)
        {
            this.busId = busId;
            this.routeId = routeId;
            this.firebase = firebase;
            this.httpClient = httpClient;
        }
        
        public async Task StartSimulation()
        {
            // Încarcă stațiile pentru traseu
            await LoadStations();
            
            if (stations.Count == 0)
            {
                Console.WriteLine($"❌ Bus {busId}: No stations found for route {routeId}");
                return;
            }
            
            Console.WriteLine($"✅ Bus {busId} (Route {routeId}): Loaded {stations.Count} stations");
            
            // Calculează traseul folosind GTFS shapes (cu fallback la OSRM)
            await CalculateRoutePoints();
            
            if (routePoints.Count == 0)
            {
                Console.WriteLine($"❌ Bus {busId}: Failed to calculate route");
                return;
            }
            
            Console.WriteLine($"✅ Bus {busId}: Route calculated with {routePoints.Count} points");
            
            while (true)
            {
                try
                {
                    var location = GetCurrentLocation();
                    
                    // Trimite locația la Firebase
                    await firebase
                        .Child("bus_locations")
                        .Child(busId.ToString())
                        .PutAsync(new
                        {
                            latitude = location.Latitude,
                            longitude = location.Longitude,
                            routeId = routeId,
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            speed = 35.0 + new Random().NextDouble() * 15.0, // 35-50 km/h
                            heading = CalculateHeading()
                        });
                    
                    Console.WriteLine($"📍 Bus {busId}: [{location.Latitude:F6}, {location.Longitude:F6}] " +
                                    $"Point {currentPointIndex + 1}/{routePoints.Count}");
                    
                    // Mută autobuzul la următorul punct
                    MoveToNextPoint();
                    
                    await Task.Delay(500); // 0.5 second
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Bus {busId} error: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }
        
        private async Task LoadStations()
        {
            try
            {
                var response = await httpClient.GetStringAsync($"{Program.ApiUrl}/routes/{routeId}/stations");
                stations = JsonConvert.DeserializeObject<List<Station>>(response) ?? new List<Station>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load stations for route {routeId}: {ex.Message}");
            }
        }
        
        private async Task CalculateRoutePoints()
        {
            try
            {
                // Încearcă să folosești GTFS shapes mai întâi
                Console.WriteLine($"🗺️  Bus {busId}: Loading GTFS shape for route {routeId}...");
                
                var shapeResponse = await httpClient.GetStringAsync($"{Program.ApiUrl}/shapes/route/{routeId}");
                var shapeData = JsonConvert.DeserializeObject<dynamic>(shapeResponse);
                
                if (shapeData?.points != null && shapeData.points.Count > 0)
                {
                    // Folosește punctele din GTFS shapes
                    foreach (var point in shapeData.points)
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
            
            // Fallback: folosește OSRM
            try
            {
                // Construiește URL pentru OSRM API
                var coordinates = string.Join(";", stations.Select(s => $"{s.Longitude},{s.Latitude}"));
                var url = $"https://router.project-osrm.org/route/v1/driving/{coordinates}" +
                          $"?overview=full&geometries=geojson";
                
                var response = await httpClient.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                
                if (data?.code == "Ok" && data?.routes != null && data.routes.Count > 0)
                {
                    var geometry = data.routes[0].geometry.coordinates;
                    
                    // Adaugă toate punctele din traseu
                    foreach (var coord in geometry)
                    {
                        // OSRM returnează [lon, lat], convertim la (lat, lon)
                        double lon = coord[0];
                        double lat = coord[1];
                        routePoints.Add((lat, lon));
                    }
                    
                    Console.WriteLine($"   ✅ Loaded {routePoints.Count} OSRM route points");
                }
                else
                {
                    Console.WriteLine($"⚠️ Bus {busId}: OSRM failed, using straight lines");
                    // Fallback final: folosim direct stațiile
                    routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Bus {busId}: OSRM error: {ex.Message}");
                // Fallback final: folosim direct stațiile
                routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
            }
        }
        
        private (double Latitude, double Longitude) GetCurrentLocation()
        {
            if (routePoints.Count == 0)
                return (45.7970, 24.1523); // Centrul Sibiului
            
            return routePoints[currentPointIndex];
        }
        
        private void MoveToNextPoint()
        {
            currentPointIndex = (currentPointIndex + 1) % routePoints.Count;
        }
        
        private double CalculateHeading()
        {
            if (routePoints.Count < 2)
                return 0;
            
            var current = routePoints[currentPointIndex];
            var next = routePoints[(currentPointIndex + 1) % routePoints.Count];
            
            var dLon = next.Longitude - current.Longitude;
            var dLat = next.Latitude - current.Latitude;
            
            var heading = Math.Atan2(dLon, dLat) * 180 / Math.PI;
            return (heading + 360) % 360; // Normalize to 0-360
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
