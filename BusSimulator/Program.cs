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
            
            // Simulăm 3 autobuze
            var bus1 = new BusSimulator(1, 1, firebaseClient, httpClient); // Bus ID 1, Route 1
            var bus2 = new BusSimulator(2, 2, firebaseClient, httpClient); // Bus ID 2, Route 2 (Linia 11)
            var bus3 = new BusSimulator(3, 3, firebaseClient, httpClient); // Bus ID 3, Route 3 (Linia 2)
            
            Console.WriteLine("✅ Simulator initialized!");
            Console.WriteLine("📡 Sending location updates every 3 seconds...");
            Console.WriteLine("Press Ctrl+C to stop.\n");
            
            // Rulează toate simulatoarele în paralel
            var task1 = bus1.StartSimulation();
            var task2 = bus2.StartSimulation();
            var task3 = bus3.StartSimulation();
            
            await Task.WhenAll(task1, task2, task3);
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
            
            // Calculează traseul pe străzi folosind OSRM
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
                // Construiește URL pentru OSRM API cu opțiuni avansate
                var coordinates = string.Join(";", stations.Select(s => $"{s.Longitude},{s.Latitude}"));
                
                // Parametri avansați:
                // - steps=true: returnează instrucțiuni detaliate
                // - geometries=geojson: format coordonate
                // - overview=full: traseu complet
                // - continue_straight=false: evită U-turn-uri aiurea
                // - annotations=true: adaugă informații despre segmente
                var url = $"https://router.project-osrm.org/route/v1/driving/{coordinates}" +
                          $"?overview=full&geometries=geojson&steps=true&continue_straight=false&annotations=true";
                
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
                    
                    // Afișează informații despre traseu
                    var distance = (double)data.routes[0].distance / 1000.0; // km
                    var duration = (double)data.routes[0].duration / 60.0; // minute
                    Console.WriteLine($"   📏 Distance: {distance:F2} km, Duration: {duration:F1} min");
                }
                else
                {
                    Console.WriteLine($"⚠️ Bus {busId}: OSRM failed, using straight lines");
                    // Fallback: folosim direct stațiile
                    routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Bus {busId}: Route calculation error: {ex.Message}");
                // Fallback: folosim direct stațiile
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
}
