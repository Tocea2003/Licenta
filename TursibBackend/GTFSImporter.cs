using System;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace TursibBackend
{
    public class GTFSImporter
    {
        private readonly string connectionString;
        private readonly string gtfsPath;

        public GTFSImporter(string connectionString, string gtfsPath)
        {
            this.connectionString = connectionString;
            this.gtfsPath = gtfsPath;
        }

        public void ImportAll()
        {
            Console.WriteLine("🚀 Starting GTFS import...");
            
            CreateTables();
            ClearExistingData();
            ImportRoutes();
            ImportStops();
            ImportShapes();
            ImportTrips();
            ImportStopTimes();
            ImportRouteStations();
            UpdateBusRoutes();
            
            Console.WriteLine("✅ GTFS import completed!");
        }

        private void CreateTables()
        {
            Console.WriteLine("📋 Creating tables...");
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                
                // Create Shapes table
                var createShapesSql = @"
                CREATE TABLE IF NOT EXISTS Shapes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShapeId TEXT NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    Sequence INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Shapes_ShapeId ON Shapes(ShapeId);";
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = createShapesSql;
                    cmd.ExecuteNonQuery();
                }

                // Create Trips table
                var createTripsSql = @"
                CREATE TABLE IF NOT EXISTS Trips (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TripId TEXT NOT NULL,
                    RouteId INTEGER NOT NULL,
                    ServiceId TEXT,
                    TripHeadsign TEXT,
                    DirectionId INTEGER,
                    ShapeId TEXT,
                    FOREIGN KEY (RouteId) REFERENCES Routes(Id)
                );
                CREATE INDEX IF NOT EXISTS IX_Trips_TripId ON Trips(TripId);
                CREATE INDEX IF NOT EXISTS IX_Trips_RouteId ON Trips(RouteId);";
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = createTripsSql;
                    cmd.ExecuteNonQuery();
                }

                // Create StopTimes table
                var createStopTimesSql = @"
                CREATE TABLE IF NOT EXISTS StopTimes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TripId TEXT NOT NULL,
                    ArrivalTime TEXT NOT NULL,
                    DepartureTime TEXT NOT NULL,
                    StopId INTEGER NOT NULL,
                    StopSequence INTEGER NOT NULL,
                    FOREIGN KEY (StopId) REFERENCES Stations(Id)
                );
                CREATE INDEX IF NOT EXISTS IX_StopTimes_TripId ON StopTimes(TripId);
                CREATE INDEX IF NOT EXISTS IX_StopTimes_StopId ON StopTimes(StopId);";
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = createStopTimesSql;
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("✅ Tables created");
        }

        private void ClearExistingData()
        {
            Console.WriteLine("🗑️  Clearing existing data...");
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                var commands = new[]
                {
                    "DELETE FROM StopTimes",
                    "DELETE FROM Trips",
                    "DELETE FROM Shapes",
                    "DELETE FROM RouteStations",
                    "DELETE FROM Stations",
                    "DELETE FROM Routes"
                };

                foreach (var cmd in commands)
                {
                    try
                    {
                        using (var command = conn.CreateCommand())
                        {
                            command.CommandText = cmd;
                            command.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: {ex.Message}");
                    }
                }
            }
            Console.WriteLine("✅ Data cleared");
        }

        private void ImportRoutes()
        {
            Console.WriteLine("📍 Importing routes...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(Path.Combine(gtfsPath, "routes.txt")))
            using (var csv = new CsvReader(reader, config))
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                var records = csv.GetRecords<dynamic>().ToList();
                
                int successCount = 0;
                foreach (var record in records)
                {
                    try
                    {
                        var dict = (IDictionary<string, object>)record;
                        var routeId = dict["route_id"].ToString();
                        var shortName = dict["route_short_name"].ToString();
                        var longName = dict["route_long_name"].ToString();
                        var color = dict["route_color"].ToString();
                        
                        var sql = @"INSERT OR REPLACE INTO Routes (Id, RouteNumber, Name, Color) 
                                    VALUES (@Id, @RouteNumber, @Name, @Color)";
                        
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql;
                            cmd.Parameters.Add(new SqliteParameter("@Id", int.Parse(routeId)));
                            cmd.Parameters.Add(new SqliteParameter("@RouteNumber", shortName));
                            cmd.Parameters.Add(new SqliteParameter("@Name", longName));
                            cmd.Parameters.Add(new SqliteParameter("@Color", "#" + color));
                            cmd.ExecuteNonQuery();
                        }
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ Error importing route: {ex.Message}");
                    }
                }
                Console.WriteLine($"✅ Imported {successCount}/{records.Count} routes");
            }
        }

        private void ImportStops()
        {
            Console.WriteLine("🚏 Importing stops...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(Path.Combine(gtfsPath, "stops.txt")))
            using (var csv = new CsvReader(reader, config))
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                var records = csv.GetRecords<dynamic>().ToList();
                
                foreach (var record in records)
                {
                    var dict = (IDictionary<string, object>)record;
                    var stopId = dict["stop_id"].ToString();
                    var stopName = dict["stop_name"].ToString();
                    var lat = double.Parse(dict["stop_lat"].ToString(), CultureInfo.InvariantCulture);
                    var lon = double.Parse(dict["stop_lon"].ToString(), CultureInfo.InvariantCulture);
                    
                    var sql = @"INSERT INTO Stations (Id, Name, Latitude, Longitude) 
                                VALUES (@Id, @Name, @Latitude, @Longitude)";
                    
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.Add(new SqliteParameter("@Id", int.Parse(stopId)));
                        cmd.Parameters.Add(new SqliteParameter("@Name", stopName));
                        cmd.Parameters.Add(new SqliteParameter("@Latitude", lat));
                        cmd.Parameters.Add(new SqliteParameter("@Longitude", lon));
                        cmd.ExecuteNonQuery();
                    }
                }
                Console.WriteLine($"✅ Imported {records.Count} stops");
            }
        }

        private void ImportShapes()
        {
            Console.WriteLine("🗺️  Importing shapes...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(Path.Combine(gtfsPath, "shapes.txt")))
            using (var csv = new CsvReader(reader, config))
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                
                using (var transaction = conn.BeginTransaction())
                {
                    var records = csv.GetRecords<dynamic>().ToList();
                    var count = 0;
                    
                    foreach (var record in records)
                    {
                        var dict = (IDictionary<string, object>)record;
                        var shapeId = dict["shape_id"].ToString();
                        var lat = double.Parse(dict["shape_pt_lat"].ToString(), CultureInfo.InvariantCulture);
                        var lon = double.Parse(dict["shape_pt_lon"].ToString(), CultureInfo.InvariantCulture);
                        var seq = int.Parse(dict["shape_pt_sequence"].ToString());
                        
                        var sql = @"INSERT INTO Shapes (ShapeId, Latitude, Longitude, Sequence) 
                                    VALUES (@ShapeId, @Latitude, @Longitude, @Sequence)";
                        
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql;
                            cmd.Parameters.Add(new SqliteParameter("@ShapeId", shapeId));
                            cmd.Parameters.Add(new SqliteParameter("@Latitude", lat));
                            cmd.Parameters.Add(new SqliteParameter("@Longitude", lon));
                            cmd.Parameters.Add(new SqliteParameter("@Sequence", seq));
                            cmd.ExecuteNonQuery();
                        }
                        
                        count++;
                        if (count % 5000 == 0)
                        {
                            Console.WriteLine($"  Imported {count} shape points...");
                        }
                    }
                    
                    transaction.Commit();
                    Console.WriteLine($"✅ Imported {records.Count} shape points");
                }
            }
        }

        private void ImportTrips()
        {
            Console.WriteLine("🚌 Importing trips...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(Path.Combine(gtfsPath, "trips.txt")))
            using (var csv = new CsvReader(reader, config))
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                
                using (var transaction = conn.BeginTransaction())
                {
                    var records = csv.GetRecords<dynamic>().ToList();
                    
                    foreach (var record in records)
                    {
                        var dict = (IDictionary<string, object>)record;
                        var tripId = dict["trip_id"].ToString();
                        var routeId = int.Parse(dict["route_id"].ToString());
                        var serviceId = dict["service_id"].ToString();
                        var headsign = dict["trip_headsign"].ToString();
                        var directionId = int.Parse(dict["direction_id"].ToString());
                        var shapeId = dict["shape_id"].ToString();
                        
                        var sql = @"INSERT INTO Trips (TripId, RouteId, ServiceId, TripHeadsign, DirectionId, ShapeId) 
                                    VALUES (@TripId, @RouteId, @ServiceId, @TripHeadsign, @DirectionId, @ShapeId)";
                        
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql;
                            cmd.Parameters.Add(new SqliteParameter("@TripId", tripId));
                            cmd.Parameters.Add(new SqliteParameter("@RouteId", routeId));
                            cmd.Parameters.Add(new SqliteParameter("@ServiceId", serviceId));
                            cmd.Parameters.Add(new SqliteParameter("@TripHeadsign", headsign));
                            cmd.Parameters.Add(new SqliteParameter("@DirectionId", directionId));
                            cmd.Parameters.Add(new SqliteParameter("@ShapeId", shapeId));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    
                    transaction.Commit();
                    Console.WriteLine($"✅ Imported {records.Count} trips");
                }
            }
        }

        private void ImportStopTimes()
        {
            Console.WriteLine("⏰ Importing stop times...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(Path.Combine(gtfsPath, "stop_times.txt")))
            using (var csv = new CsvReader(reader, config))
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                
                using (var transaction = conn.BeginTransaction())
                {
                    var records = csv.GetRecords<dynamic>().ToList();
                    var count = 0;
                    
                    foreach (var record in records)
                    {
                        var dict = (IDictionary<string, object>)record;
                        var tripId = dict["trip_id"].ToString();
                        var arrivalTime = dict["arrival_time"].ToString();
                        var departureTime = dict["departure_time"].ToString();
                        var stopId = int.Parse(dict["stop_id"].ToString());
                        var stopSeq = int.Parse(dict["stop_sequence"].ToString());
                        
                        var sql = @"INSERT INTO StopTimes (TripId, ArrivalTime, DepartureTime, StopId, StopSequence) 
                                    VALUES (@TripId, @ArrivalTime, @DepartureTime, @StopId, @StopSequence)";
                        
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql;
                            cmd.Parameters.Add(new SqliteParameter("@TripId", tripId));
                            cmd.Parameters.Add(new SqliteParameter("@ArrivalTime", arrivalTime));
                            cmd.Parameters.Add(new SqliteParameter("@DepartureTime", departureTime));
                            cmd.Parameters.Add(new SqliteParameter("@StopId", stopId));
                            cmd.Parameters.Add(new SqliteParameter("@StopSequence", stopSeq));
                            cmd.ExecuteNonQuery();
                        }
                        
                        count++;
                        if (count % 5000 == 0)
                        {
                            Console.WriteLine($"  Imported {count} stop times...");
                        }
                    }
                    
                    transaction.Commit();
                    Console.WriteLine($"✅ Imported {records.Count} stop times");
                }
            }
        }

        private void ImportRouteStations()
        {
            Console.WriteLine("🔗 Creating route-station relationships...");
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                // Pentru fiecare traseu, găsește o cursă reprezentativă și extrage stațiile în ordine
                var routes = conn.CreateCommand();
                routes.CommandText = "SELECT DISTINCT RouteId FROM Trips";
                
                using (var reader = routes.ExecuteReader())
                {
                    var routeIds = new List<int>();
                    while (reader.Read())
                    {
                        routeIds.Add(reader.GetInt32(0));
                    }
                    
                    int totalStations = 0;
                    foreach (var routeId in routeIds)
                    {
                        // Găsește prima cursă pentru acest traseu
                        var tripCmd = conn.CreateCommand();
                        tripCmd.CommandText = "SELECT TripId FROM Trips WHERE RouteId = @RouteId LIMIT 1";
                        tripCmd.Parameters.Add(new SqliteParameter("@RouteId", routeId));
                        
                        var tripId = tripCmd.ExecuteScalar()?.ToString();
                        if (string.IsNullOrEmpty(tripId))
                        {
                            Console.WriteLine($"  ⚠️ No trip found for route {routeId}");
                            continue;
                        }
                        
                        // Obține stațiile în ordine pentru această cursă
                        var stopsCmd = conn.CreateCommand();
                        stopsCmd.CommandText = @"
                            SELECT StopId, StopSequence 
                            FROM StopTimes 
                            WHERE TripId = @TripId 
                            ORDER BY StopSequence";
                        stopsCmd.Parameters.Add(new SqliteParameter("@TripId", tripId));
                        
                        int routeStationCount = 0;
                        using (var stopsReader = stopsCmd.ExecuteReader())
                        {
                            var order = 1;  // Start from 1, not 0
                            while (stopsReader.Read())
                            {
                                var stopId = stopsReader.GetInt32(0);
                                
                                // Inserează în RouteStations
                                var insertCmd = conn.CreateCommand();
                                insertCmd.CommandText = @"
                                    INSERT OR IGNORE INTO RouteStations (RouteId, StationId, ""Order"") 
                                    VALUES (@RouteId, @StationId, @Order)";
                                insertCmd.Parameters.Add(new SqliteParameter("@RouteId", routeId));
                                insertCmd.Parameters.Add(new SqliteParameter("@StationId", stopId));
                                insertCmd.Parameters.Add(new SqliteParameter("@Order", order));
                                
                                try
                                {
                                    var affected = insertCmd.ExecuteNonQuery();
                                    if (affected > 0)
                                    {
                                        routeStationCount++;
                                        totalStations++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"  ⚠️ Error inserting route {routeId}, station {stopId}, order {order}: {ex.Message}");
                                }
                                
                                order++;
                            }
                        }
                        
                        if (routeStationCount > 0)
                        {
                            Console.WriteLine($"  ✓ Route {routeId}: {routeStationCount} stations");
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠️ Route {routeId}: No stations added");
                        }
                    }
                    
                    Console.WriteLine($"✅ Created {totalStations} route-station relationships for {routeIds.Count} routes");
                }
            }
        }

        private void UpdateBusRoutes()
        {
            Console.WriteLine("🚌 Updating bus routes...");
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                // Mapare manuală: Autobuz ID → Route ID din GTFS
                var busRouteMapping = new Dictionary<int, int>
                {
                    { 1, 1 },   // Autobuz 1 → Linia 1
                    { 2, 11 },  // Autobuz 2 → Linia 11
                    { 3, 2 }    // Autobuz 3 → Linia 2
                };

                foreach (var mapping in busRouteMapping)
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Buses SET CurrentRouteId = @RouteId WHERE Id = @BusId";
                    cmd.Parameters.Add(new SqliteParameter("@RouteId", mapping.Value));
                    cmd.Parameters.Add(new SqliteParameter("@BusId", mapping.Key));
                    
                    var affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                    {
                        Console.WriteLine($"  ✓ Bus {mapping.Key} → Route {mapping.Value}");
                    }
                }
                
                Console.WriteLine("✅ Bus routes updated");
            }
        }
    }
}
