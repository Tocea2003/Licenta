using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend
{
    /// <summary>
    /// Importă un feed GTFS (routes, stops, shapes, trips, calendar, stop_times)
    /// în baza de date, prin EF Core. Totul rulează într-o singură tranzacție:
    /// dacă importul eșuează, datele vechi rămân neatinse.
    /// </summary>
    public class GTFSImporter
    {
        private const int BatchSize = 5000;

        private readonly ApplicationDbContext _db;
        private readonly string _gtfsPath;

        public GTFSImporter(ApplicationDbContext db, string gtfsPath)
        {
            _db = db;
            _gtfsPath = gtfsPath;
        }

        public async Task ImportAllAsync()
        {
            if (!Directory.Exists(_gtfsPath))
                throw new DirectoryNotFoundException($"GTFS directory not found: {_gtfsPath}");

            Console.WriteLine("🚀 Starting GTFS import...");
            _db.ChangeTracker.AutoDetectChangesEnabled = false;

            // Strategia cu retry (EnableRetryOnFailure) cere ca tranzacția să fie o unitate re-executabilă
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync();

                await ClearExistingData();
                await ImportRoutes();
                await ImportStops();
                await ImportShapes();
                await ImportTrips();
                await ImportCalendar();
                var stopTimes = await ImportStopTimes();
                await ImportRouteStations(stopTimes);
                await UpdateBusRoutes();
                await DatabaseMaintenance.ResetIdentitySequencesAsync(_db);

                await transaction.CommitAsync();
            });
            Console.WriteLine("✅ GTFS import completed!");
        }

        private async Task ClearExistingData()
        {
            Console.WriteLine("🗑️  Clearing existing data...");
            // Autobuzele referă rutele: le detașăm înainte de ștergere
            await _db.Buses.ExecuteUpdateAsync(b => b.SetProperty(x => x.CurrentRouteId, (int?)null));
            await _db.StopTimes.ExecuteDeleteAsync();
            await _db.Trips.ExecuteDeleteAsync();
            await _db.ShapePoints.ExecuteDeleteAsync();
            await _db.ServiceCalendars.ExecuteDeleteAsync();
            await _db.RouteStations.ExecuteDeleteAsync();
            await _db.Stations.ExecuteDeleteAsync();
            await _db.Routes.ExecuteDeleteAsync();
        }

        private async Task ImportRoutes()
        {
            var routes = ReadCsv("routes.txt").Select(r => new RouteModel
            {
                Id = int.Parse(r["route_id"]),
                RouteNumber = r["route_short_name"],
                Name = r["route_long_name"],
                Color = "#" + r["route_color"]
            }).ToList();

            await InsertInBatches(routes);
            Console.WriteLine($"✅ Imported {routes.Count} routes");
        }

        private async Task ImportStops()
        {
            var stations = ReadCsv("stops.txt").Select(r => new Station
            {
                Id = int.Parse(r["stop_id"]),
                Name = r["stop_name"],
                Latitude = ParseDouble(r["stop_lat"]),
                Longitude = ParseDouble(r["stop_lon"])
            }).ToList();

            await InsertInBatches(stations);
            Console.WriteLine($"✅ Imported {stations.Count} stops");
        }

        private async Task ImportShapes()
        {
            var points = ReadCsv("shapes.txt").Select(r => new ShapePoint
            {
                ShapeId = r["shape_id"],
                Latitude = ParseDouble(r["shape_pt_lat"]),
                Longitude = ParseDouble(r["shape_pt_lon"]),
                Sequence = int.Parse(r["shape_pt_sequence"])
            }).ToList();

            await InsertInBatches(points);
            Console.WriteLine($"✅ Imported {points.Count} shape points");
        }

        private async Task ImportTrips()
        {
            var trips = ReadCsv("trips.txt").Select(r => new Trip
            {
                TripId = r["trip_id"],
                RouteId = int.Parse(r["route_id"]),
                ServiceId = r["service_id"],
                TripHeadsign = r.GetValueOrDefault("trip_headsign"),
                DirectionId = int.TryParse(r.GetValueOrDefault("direction_id"), out var dir) ? dir : null,
                ShapeId = r.GetValueOrDefault("shape_id")
            }).ToList();

            await InsertInBatches(trips);
            Console.WriteLine($"✅ Imported {trips.Count} trips");
        }

        private async Task ImportCalendar()
        {
            if (!File.Exists(Path.Combine(_gtfsPath, "calendar.txt")))
            {
                Console.WriteLine("  ⚠️ calendar.txt not found, skipping calendar import");
                return;
            }

            // GTFS folosește YYYYMMDD; stocăm yyyy-MM-dd pentru comparații lexicografice
            static string ToIsoDate(string raw) => $"{raw[..4]}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)}";

            var calendars = ReadCsv("calendar.txt").Select(r => new ServiceCalendar
            {
                ServiceId = r["service_id"],
                Monday = r["monday"] == "1",
                Tuesday = r["tuesday"] == "1",
                Wednesday = r["wednesday"] == "1",
                Thursday = r["thursday"] == "1",
                Friday = r["friday"] == "1",
                Saturday = r["saturday"] == "1",
                Sunday = r["sunday"] == "1",
                StartDate = ToIsoDate(r["start_date"]),
                EndDate = ToIsoDate(r["end_date"])
            }).ToList();

            await InsertInBatches(calendars);
            Console.WriteLine($"✅ Imported {calendars.Count} calendar entries");
        }

        private async Task<List<StopTime>> ImportStopTimes()
        {
            var stopTimes = ReadCsv("stop_times.txt").Select(r => new StopTime
            {
                TripId = r["trip_id"],
                ArrivalTime = r["arrival_time"],
                DepartureTime = r["departure_time"],
                StopId = int.Parse(r["stop_id"]),
                StopSequence = int.Parse(r["stop_sequence"])
            }).ToList();

            await InsertInBatches(stopTimes);
            Console.WriteLine($"✅ Imported {stopTimes.Count} stop times");
            return stopTimes;
        }

        /// <summary>
        /// Pentru fiecare traseu alege o cursă reprezentativă și salvează stațiile ei în ordine.
        /// Alegem numărul de stații cel mai frecvent (mode), apoi cea mai devreme cursă din acel grup,
        /// ca să evităm cursele speciale/outlier (ex: o singură cursă extinsă).
        /// </summary>
        private async Task ImportRouteStations(List<StopTime> stopTimes)
        {
            Console.WriteLine("🔗 Creating route-station relationships...");
            var trips = await _db.Trips.AsNoTracking().ToListAsync();
            var stopsByTrip = stopTimes
                .GroupBy(st => st.TripId)
                .ToDictionary(g => g.Key, g => g.OrderBy(st => st.StopSequence).ToList());

            var routeStations = new List<RouteStation>();
            foreach (var routeTrips in trips.GroupBy(t => t.RouteId))
            {
                var stats = routeTrips
                    .Where(t => stopsByTrip.ContainsKey(t.TripId))
                    .Select(t => new
                    {
                        Stops = stopsByTrip[t.TripId],
                        FirstDeparture = stopsByTrip[t.TripId].Min(st => st.DepartureTime)
                    })
                    .ToList();
                if (stats.Count == 0)
                {
                    Console.WriteLine($"  ⚠️ No trip found for route {routeTrips.Key}");
                    continue;
                }

                var bestStopCount = stats
                    .GroupBy(s => s.Stops.Count)
                    .OrderByDescending(g => g.Count())
                    .ThenByDescending(g => g.Key)
                    .First().Key;

                var representative = stats
                    .Where(s => s.Stops.Count == bestStopCount)
                    .OrderBy(s => s.FirstDeparture, StringComparer.Ordinal)
                    .First();

                var order = 1;
                foreach (var stop in representative.Stops)
                {
                    routeStations.Add(new RouteStation { RouteId = routeTrips.Key, StationId = stop.StopId, Order = order++ });
                }
            }

            await InsertInBatches(routeStations);
            Console.WriteLine($"✅ Created {routeStations.Count} route-station relationships");
        }

        private async Task UpdateBusRoutes()
        {
            // Mapare manuală: Autobuz ID → Route ID din GTFS
            var busRouteMapping = new Dictionary<int, int>
            {
                { 1, 1 },   // Autobuz 1 → Linia 1
                { 2, 11 },  // Autobuz 2 → Linia 11
                { 3, 2 }    // Autobuz 3 → Linia 2
            };

            foreach (var (busId, routeId) in busRouteMapping)
            {
                if (!await _db.Routes.AnyAsync(r => r.Id == routeId))
                    continue;
                await _db.Buses
                    .Where(b => b.Id == busId)
                    .ExecuteUpdateAsync(b => b.SetProperty(x => x.CurrentRouteId, routeId));
            }
        }

        private async Task InsertInBatches<T>(List<T> items) where T : class
        {
            for (int i = 0; i < items.Count; i += BatchSize)
            {
                _db.AddRange(items.Skip(i).Take(BatchSize));
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();
            }
        }

        private List<Dictionary<string, string>> ReadCsv(string fileName)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(Path.Combine(_gtfsPath, fileName));
            using var csv = new CsvReader(reader, config);

            return csv.GetRecords<dynamic>()
                .Select(record => ((IDictionary<string, object>)record)
                    .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty))
                .ToList();
        }

        private static double ParseDouble(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    }
}
