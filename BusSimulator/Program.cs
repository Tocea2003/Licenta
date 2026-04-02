using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using Microsoft.Data.Sqlite;

namespace BusSimulator
{
    class Program
    {
        private const string FirebaseUrl = "https://licenta-ulbs-default-rtdb.europe-west1.firebasedatabase.app/";
        public const string ApiUrl = "http://localhost:5022/api";

        // Find DB path: tries paths relative to the assembly and CWD
        public static readonly string DbPath = FindDbPath();

        private static string FindDbPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TursibBackend", "TursibDb.db"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TursibBackend", "TursibDb.db"),
                Path.Combine("..", "TursibBackend", "TursibDb.db"),
                Path.Combine("TursibBackend", "TursibDb.db"),
            };
            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            return "";
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚌 Tursib Bus Simulator - Starting...");
            Console.WriteLine("========================================");

            if (string.IsNullOrEmpty(DbPath))
                Console.WriteLine("⚠️  Database not found – buses will run without schedule");
            else
                Console.WriteLine($"📂 DB: {DbPath}");

            var firebaseClient = new FirebaseClient(FirebaseUrl);
            var httpClient = new HttpClient();

            var routes = await LoadAllRoutes(httpClient);
            if (routes.Count == 0)
            {
                Console.WriteLine("❌ No routes found!");
                return;
            }

            Console.WriteLine($"📍 Found {routes.Count} routes in GTFS");

            var simulators = new List<Task>();
            var busId = 1;

            foreach (var route in routes)
            {
                var simulator = new BusSimulatorInstance(busId++, route.Id, firebaseClient, httpClient);
                simulators.Add(simulator.StartSimulation());
                await Task.Delay(200);
            }

            Console.WriteLine($"✅ {simulators.Count} simulators initialized!");
            Console.WriteLine("📡 Sending location updates...");
            Console.WriteLine("Press Ctrl+C to stop.\n");

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

    // ─── Schedule data structures ───────────────────────────────────────────────

    class StopTimeEntry
    {
        public int StopId { get; set; }
        public int TimeSeconds { get; set; }   // seconds from midnight (can exceed 86400)
        public int ShapePointIndex { get; set; } // nearest shape point, pre-computed
    }

    class TripSchedule
    {
        public string TripId { get; set; } = "";
        public List<StopTimeEntry> Stops { get; set; } = new();
        public int StartSeconds => Stops.Count > 0 ? Stops[0].TimeSeconds : 0;
        public int EndSeconds   => Stops.Count > 0 ? Stops[^1].TimeSeconds : 0;
    }

    // ─── Main simulator class ────────────────────────────────────────────────────

    class BusSimulatorInstance
    {
        private readonly int busId;
        private readonly int routeId;
        private readonly FirebaseClient firebase;
        private readonly HttpClient httpClient;

        private List<Station> stations = new();
        private List<(double Latitude, double Longitude)> routePoints = new();
        private List<TripSchedule> tripSchedules = new();

        private int occupancy = 0;
        private readonly Random random = new();

        public BusSimulatorInstance(int busId, int routeId, FirebaseClient firebase, HttpClient httpClient)
        {
            this.busId = busId;
            this.routeId = routeId;
            this.firebase = firebase;
            this.httpClient = httpClient;
        }

        // ── Simulation entry point ───────────────────────────────────────────────

        public async Task StartSimulation()
        {
            await LoadStations();
            if (stations.Count == 0)
            {
                Console.WriteLine($"❌ Bus {busId}: No stations for route {routeId}");
                return;
            }

            await CalculateRoutePoints();
            if (routePoints.Count == 0)
            {
                Console.WriteLine($"❌ Bus {busId}: Failed to calculate route");
                return;
            }

            LoadScheduleFromDb();

            Console.WriteLine($"✅ Bus {busId} (Route {routeId}): {stations.Count} stations, " +
                              $"{routePoints.Count} shape pts, {tripSchedules.Count} trips");

            int fallbackIndex = 0; // used when no trip is active

            while (true)
            {
                try
                {
                    int? shapeIdx = GetScheduledShapeIndex();

                    if (!shapeIdx.HasValue)
                    {
                        // Bus is not in service right now – wait quietly
                        await Task.Delay(5000);
                        continue;
                    }

                    var location = routePoints[shapeIdx.Value];
                    fallbackIndex = shapeIdx.Value;

                    if (IsNearStation(location))
                        UpdateOccupancy();

                    await SendLocationWithRetry(location, shapeIdx.Value);

                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Bus {busId} error: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }

        // ── Schedule loading ─────────────────────────────────────────────────────

        private void LoadScheduleFromDb()
        {
            if (string.IsNullOrEmpty(Program.DbPath)) return;

            try
            {
                using var conn = new SqliteConnection($"Data Source={Program.DbPath}");
                conn.Open();

                // Pull all stop times for every trip of this route, ordered
                var sql = @"
                    SELECT t.TripId, st.StopId, st.DepartureTime, st.StopSequence
                    FROM Trips t
                    JOIN StopTimes st ON st.TripId = t.TripId
                    WHERE t.RouteId = @RouteId
                    ORDER BY t.TripId, st.StopSequence";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.Add(new SqliteParameter("@RouteId", routeId));

                var tripsDict = new Dictionary<string, List<(int StopId, int Seconds, int Seq)>>();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tripId   = reader.GetString(0);
                        var stopId   = reader.GetInt32(1);
                        var timeStr  = reader.GetString(2);
                        var seq      = reader.GetInt32(3);

                        if (!tripsDict.ContainsKey(tripId))
                            tripsDict[tripId] = new();

                        tripsDict[tripId].Add((stopId, ParseGtfsTime(timeStr), seq));
                    }
                }

                // Build station → nearest shape-point index
                var stationShapeMap = BuildStationShapeMap();

                foreach (var (tripId, stops) in tripsDict)
                {
                    var trip = new TripSchedule { TripId = tripId };

                    foreach (var (stopId, seconds, _) in stops.OrderBy(s => s.Seq))
                    {
                        if (stationShapeMap.TryGetValue(stopId, out int shapeIdx))
                        {
                            trip.Stops.Add(new StopTimeEntry
                            {
                                StopId         = stopId,
                                TimeSeconds    = seconds,
                                ShapePointIndex = shapeIdx
                            });
                        }
                    }

                    if (trip.Stops.Count >= 2)
                        tripSchedules.Add(trip);
                }

                Console.WriteLine($"   📅 Bus {busId}: {tripSchedules.Count} scheduled trips loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Bus {busId}: Schedule load failed: {ex.Message}");
            }
        }

        // Maps each station ID → index of nearest routePoints entry
        private Dictionary<int, int> BuildStationShapeMap()
        {
            var map = new Dictionary<int, int>();
            foreach (var station in stations)
            {
                double minDist = double.MaxValue;
                int nearestIdx = 0;

                for (int i = 0; i < routePoints.Count; i++)
                {
                    var pt = routePoints[i];
                    double d = Math.Pow(pt.Latitude - station.Latitude, 2)
                             + Math.Pow(pt.Longitude - station.Longitude, 2);
                    if (d < minDist) { minDist = d; nearestIdx = i; }
                }

                map[station.Id] = nearestIdx;
            }
            return map;
        }

        // ── Schedule-based position calculation ──────────────────────────────────

        private int? GetScheduledShapeIndex()
        {
            if (tripSchedules.Count == 0)
                return null;

            var now = DateTime.Now;
            int currentSeconds = now.Hour * 3600 + now.Minute * 60 + now.Second;

            // Find the trip that is currently running and started most recently
            TripSchedule? activeTrip = null;
            int latestStart = -1;

            foreach (var trip in tripSchedules)
            {
                if (trip.StartSeconds <= currentSeconds && currentSeconds <= trip.EndSeconds
                    && trip.StartSeconds > latestStart)
                {
                    latestStart  = trip.StartSeconds;
                    activeTrip   = trip;
                }
            }

            if (activeTrip == null) return null;

            var stops = activeTrip.Stops;

            // Find the segment [stopA, stopB] that brackets the current time
            for (int i = 0; i < stops.Count - 1; i++)
            {
                var a = stops[i];
                var b = stops[i + 1];

                if (currentSeconds >= a.TimeSeconds && currentSeconds < b.TimeSeconds)
                {
                    double fraction = (double)(currentSeconds - a.TimeSeconds)
                                    / Math.Max(1, b.TimeSeconds - a.TimeSeconds);

                    int idxA = a.ShapePointIndex;
                    int idxB = b.ShapePointIndex;

                    // Guard against reversed indices (can happen at route wrap)
                    if (idxB <= idxA)
                        idxB = Math.Min(idxA + 5, routePoints.Count - 1);

                    int shapeIdx = (int)(idxA + fraction * (idxB - idxA));
                    return Math.Clamp(shapeIdx, 0, routePoints.Count - 1);
                }
            }

            // Past the last stop
            return stops[^1].ShapePointIndex;
        }

        // Parses "HH:MM:SS" including times >= 24:00:00
        private static int ParseGtfsTime(string timeStr)
        {
            var parts = timeStr.Split(':');
            if (parts.Length < 2) return 0;
            return int.Parse(parts[0]) * 3600
                 + int.Parse(parts[1]) * 60
                 + (parts.Length > 2 ? int.Parse(parts[2]) : 0);
        }

        // ── Firebase ─────────────────────────────────────────────────────────────

        private async Task SendLocationWithRetry(
            (double Latitude, double Longitude) location,
            int shapeIdx,
            int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await firebase
                        .Child("bus_locations")
                        .Child(busId.ToString())
                        .PutAsync(new
                        {
                            latitude  = location.Latitude,
                            longitude = location.Longitude,
                            routeId   = routeId,
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            speed     = 35.0 + random.NextDouble() * 15.0,
                            heading   = CalculateHeading(shapeIdx),
                            occupancy = occupancy
                        });
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                        Console.WriteLine($"❌ Bus {busId} Firebase error: {ex.Message}");
                    else
                        await Task.Delay(500 * attempt);
                }
            }
        }

        // ── Route loading ────────────────────────────────────────────────────────

        private async Task LoadStations()
        {
            try
            {
                var response = await httpClient.GetStringAsync($"{Program.ApiUrl}/routes/{routeId}/stations");
                stations = JsonConvert.DeserializeObject<List<Station>>(response) ?? new();
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
                Console.WriteLine($"🗺️  Bus {busId}: Loading GTFS shape for route {routeId}...");
                var shapeResponse = await httpClient.GetStringAsync($"{Program.ApiUrl}/shapes/route/{routeId}");
                var shapeData = JsonConvert.DeserializeObject<dynamic>(shapeResponse);

                if (shapeData?.points != null && shapeData.points.Count > 0)
                {
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
                Console.WriteLine($"⚠️ Bus {busId}: GTFS shapes unavailable ({ex.Message}), trying OSRM...");
            }

            // Fallback: OSRM
            try
            {
                var coordinates = string.Join(";", stations.Select(s => $"{s.Longitude},{s.Latitude}"));
                var url = $"https://router.project-osrm.org/route/v1/driving/{coordinates}?overview=full&geometries=geojson";
                var response = await httpClient.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);

                if (data?.code == "Ok" && data?.routes != null && data.routes.Count > 0)
                {
                    foreach (var coord in data.routes[0].geometry.coordinates)
                    {
                        double lon = coord[0];
                        double lat = coord[1];
                        routePoints.Add((lat, lon));
                    }
                    Console.WriteLine($"   ✅ Loaded {routePoints.Count} OSRM route points");
                }
                else
                {
                    routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
                }
            }
            catch
            {
                routePoints = stations.Select(s => (s.Latitude, s.Longitude)).ToList();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private double CalculateHeading(int currentIdx)
        {
            if (routePoints.Count < 2) return 0;
            var current = routePoints[currentIdx];
            var next    = routePoints[Math.Min(currentIdx + 1, routePoints.Count - 1)];
            var dLon = next.Longitude - current.Longitude;
            var dLat = next.Latitude  - current.Latitude;
            return ((Math.Atan2(dLon, dLat) * 180 / Math.PI) + 360) % 360;
        }

        private bool IsNearStation((double Latitude, double Longitude) location)
        {
            const double threshold = 0.0001; // ~11 m
            return stations.Any(s =>
                Math.Abs(s.Latitude  - location.Latitude)  < threshold &&
                Math.Abs(s.Longitude - location.Longitude) < threshold);
        }

        private void UpdateOccupancy()
        {
            occupancy = Math.Clamp(occupancy + random.Next(-15, 25), 0, 100);

            var hour = DateTime.Now.Hour;
            if ((hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 18))
            {
                if (occupancy < 60) occupancy += random.Next(5, 15);
            }
            else if (hour >= 22 || hour <= 5)
            {
                if (occupancy > 30) occupancy -= random.Next(5, 10);
            }
        }
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    class Station
    {
        [JsonProperty("id")]       public int    Id        { get; set; }
        [JsonProperty("name")]     public string Name      { get; set; } = "";
        [JsonProperty("latitude")] public double Latitude  { get; set; }
        [JsonProperty("longitude")]public double Longitude { get; set; }
    }

    class RouteInfo
    {
        [JsonProperty("id")]          public int    Id          { get; set; }
        [JsonProperty("routeNumber")] public string RouteNumber { get; set; } = "";
        [JsonProperty("name")]        public string Name        { get; set; } = "";
        [JsonProperty("color")]       public string Color       { get; set; } = "";
    }
}
