using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Models;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend.Data
{
    /// <summary>
    /// Operații de întreținere a bazei de date rulate din linia de comandă
    /// (vezi Program.cs: <c>dotnet run -- copy-from-sqlite ...</c>).
    /// </summary>
    public static class DatabaseMaintenance
    {
        private const int BatchSize = 5000;

        /// <summary>
        /// După inserări cu Id explicit (GTFS, copiere), secvențele PostgreSQL rămân în urmă
        /// și următorul INSERT ar primi un Id deja folosit. Le aliniem la MAX(Id).
        /// </summary>
        public static async Task ResetIdentitySequencesAsync(ApplicationDbContext db)
        {
            if (!db.Database.IsNpgsql())
                return;

            foreach (var entityType in db.Model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table == null || entityType.FindProperty("Id")?.ClrType != typeof(int))
                    continue;

                // Numele tabelului vine din modelul EF, nu din input extern
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync(
                    $@"SELECT setval(pg_get_serial_sequence('""{table}""', 'Id'),
                                     COALESCE((SELECT MAX(""Id"") FROM ""{table}""), 0) + 1, false)");
#pragma warning restore EF1002
            }
        }

        /// <summary>
        /// Copiază toate datele dintr-un fișier SQLite vechi (TursibDb.db) în baza curentă.
        /// Baza țintă trebuie să fie goală (doar schema aplicată prin migrări).
        /// </summary>
        public static async Task CopyFromSqliteAsync(ApplicationDbContext db, string sqlitePath)
        {
            if (!File.Exists(sqlitePath))
                throw new FileNotFoundException($"SQLite file not found: {sqlitePath}");

            if (await db.Routes.AnyAsync() || await db.Users.AnyAsync())
                throw new InvalidOperationException(
                    "Target database is not empty. Copy only into a freshly migrated database.");

            db.ChangeTracker.AutoDetectChangesEnabled = false;

            // Strategia cu retry (EnableRetryOnFailure) cere ca tranzacția să fie o unitate re-executabilă
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var source = new SqliteConnection($"Data Source={sqlitePath};Mode=ReadOnly");
                await source.OpenAsync();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await CopyAllTables(db, source);
                await ResetIdentitySequencesAsync(db);
                await transaction.CommitAsync();
            });
        }

        private static async Task CopyAllTables(ApplicationDbContext db, SqliteConnection source)
        {

            // Ordinea respectă cheile străine
            await Copy(db, source, "Routes", r => new RouteModel
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                RouteNumber = Str(r, "RouteNumber")!,
                Name = Str(r, "Name")!,
                Color = Str(r, "Color")
            });
            await Copy(db, source, "Stations", r => new Station
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                Name = Str(r, "Name")!,
                Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
                Longitude = r.GetDouble(r.GetOrdinal("Longitude"))
            });
            await Copy(db, source, "Buses", r => new Bus
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                LicensePlate = Str(r, "LicensePlate")!,
                InternalName = Str(r, "InternalName")!,
                CurrentRouteId = NullableInt(r, "CurrentRouteId")
            });
            await Copy(db, source, "RouteStations", r => new RouteStation
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                RouteId = r.GetInt32(r.GetOrdinal("RouteId")),
                StationId = r.GetInt32(r.GetOrdinal("StationId")),
                Order = r.GetInt32(r.GetOrdinal("Order"))
            });
            await Copy(db, source, "Users", r => new User
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                Username = Str(r, "Username")!,
                PasswordHash = Str(r, "PasswordHash")!,
                Role = Str(r, "Role")!,
                Email = Str(r, "Email"),
                Favorites = Str(r, "Favorites"),
                CreatedAt = UtcDate(r, "CreatedAt")
            });
            await Copy(db, source, "Payments", r => new Payment
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                UserId = r.GetInt32(r.GetOrdinal("UserId")),
                Amount = r.GetDecimal(r.GetOrdinal("Amount")),
                Currency = Str(r, "Currency")!,
                CardLast4 = Str(r, "CardLast4")!,
                CardBrand = Str(r, "CardBrand")!,
                CardholderName = Str(r, "CardholderName")!,
                Status = Str(r, "Status")!,
                FailureReason = Str(r, "FailureReason"),
                CreatedAt = UtcDate(r, "CreatedAt")
            });
            await Copy(db, source, "Tickets", r => new Ticket
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                UserId = r.GetInt32(r.GetOrdinal("UserId")),
                TicketType = Str(r, "TicketType")!,
                PriceRon = r.GetDecimal(r.GetOrdinal("PriceRon")),
                Status = Str(r, "Status")!,
                PurchasedAt = UtcDate(r, "PurchasedAt"),
                ValidFrom = UtcDate(r, "ValidFrom"),
                ValidUntil = UtcDate(r, "ValidUntil"),
                QrToken = Str(r, "QrToken")!,
                PaymentId = NullableInt(r, "PaymentId")
            });
            await Copy(db, source, "Shapes", r => new ShapePoint
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                ShapeId = Str(r, "ShapeId")!,
                Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
                Longitude = r.GetDouble(r.GetOrdinal("Longitude")),
                Sequence = r.GetInt32(r.GetOrdinal("Sequence"))
            });
            await Copy(db, source, "Trips", r => new Trip
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                TripId = Str(r, "TripId")!,
                RouteId = r.GetInt32(r.GetOrdinal("RouteId")),
                ServiceId = Str(r, "ServiceId"),
                TripHeadsign = Str(r, "TripHeadsign"),
                DirectionId = NullableInt(r, "DirectionId"),
                ShapeId = Str(r, "ShapeId")
            });
            await Copy(db, source, "StopTimes", r => new StopTime
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                TripId = Str(r, "TripId")!,
                ArrivalTime = Str(r, "ArrivalTime")!,
                DepartureTime = Str(r, "DepartureTime")!,
                StopId = r.GetInt32(r.GetOrdinal("StopId")),
                StopSequence = r.GetInt32(r.GetOrdinal("StopSequence"))
            });
            await Copy(db, source, "ServiceCalendars", r => new ServiceCalendar
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                ServiceId = Str(r, "ServiceId")!,
                Monday = r.GetInt32(r.GetOrdinal("Monday")) == 1,
                Tuesday = r.GetInt32(r.GetOrdinal("Tuesday")) == 1,
                Wednesday = r.GetInt32(r.GetOrdinal("Wednesday")) == 1,
                Thursday = r.GetInt32(r.GetOrdinal("Thursday")) == 1,
                Friday = r.GetInt32(r.GetOrdinal("Friday")) == 1,
                Saturday = r.GetInt32(r.GetOrdinal("Saturday")) == 1,
                Sunday = r.GetInt32(r.GetOrdinal("Sunday")) == 1,
                StartDate = Str(r, "StartDate")!,
                EndDate = Str(r, "EndDate")!
            });
        }

        private static async Task Copy<T>(
            ApplicationDbContext db, SqliteConnection source, string table, Func<SqliteDataReader, T> map)
            where T : class
        {
            await using (var check = source.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
                check.Parameters.AddWithValue("$name", table);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) == 0)
                {
                    Console.WriteLine($"  ⚠️ {table}: missing in source, skipped");
                    return;
                }
            }

            await using var cmd = source.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{table}\"";
            await using var reader = await cmd.ExecuteReaderAsync();

            var batch = new List<T>(BatchSize);
            var total = 0;
            while (await reader.ReadAsync())
            {
                batch.Add(map(reader));
                if (batch.Count == BatchSize)
                {
                    total += await Flush(db, batch);
                }
            }
            total += await Flush(db, batch);
            Console.WriteLine($"  ✓ {table}: {total} rows");
        }

        private static async Task<int> Flush<T>(ApplicationDbContext db, List<T> batch) where T : class
        {
            if (batch.Count == 0) return 0;
            db.AddRange(batch);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var count = batch.Count;
            batch.Clear();
            return count;
        }

        private static string? Str(SqliteDataReader r, string column)
        {
            var ordinal = r.GetOrdinal(column);
            return r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
        }

        private static int? NullableInt(SqliteDataReader r, string column)
        {
            var ordinal = r.GetOrdinal(column);
            return r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);
        }

        // EF Core + SQLite stochează DateTime ca text; valorile au fost scrise cu DateTime.UtcNow
        private static DateTime UtcDate(SqliteDataReader r, string column) =>
            DateTime.SpecifyKind(
                DateTime.Parse(r.GetString(r.GetOrdinal(column)), CultureInfo.InvariantCulture),
                DateTimeKind.Utc);
    }
}
