// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using TursibBackend.Models;

namespace TursibBackend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Definirea tabelelor care vor fi create în baza de date
        public DbSet<Models.Route> Routes { get; set; }
        public DbSet<Station> Stations { get; set; }
        public DbSet<Bus> Buses { get; set; }
        public DbSet<RouteStation> RouteStations { get; set; }
        public DbSet<User> Users { get; set; }

        // Aici configurăm relațiile (deși EF Core le-ar putea deduce singur,
        // e bine să fim expliciți pentru relația Many-to-Many)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relația Route <-> RouteStation
            modelBuilder.Entity<RouteStation>()
                .HasOne(rs => rs.Route)          // Un RouteStation are un singur Route
                .WithMany(r => r.RouteStations) // Un Route are multe RouteStations
                .HasForeignKey(rs => rs.RouteId); // Cheia străină este RouteId

            // Relația Station <-> RouteStation
            modelBuilder.Entity<RouteStation>()
                .HasOne(rs => rs.Station)         // Un RouteStation are o singură Station
                .WithMany(s => s.RouteStations) // O Station are multe RouteStations
                .HasForeignKey(rs => rs.StationId); // Cheia străină este StationId

            // Configurare precizie pentru coordonate GPS
            modelBuilder.Entity<Station>()
                .Property(s => s.Latitude)
                .HasColumnType("REAL")
                .HasPrecision(18, 10); // Precizie mare pentru GPS
            
            modelBuilder.Entity<Station>()
                .Property(s => s.Longitude)
                .HasColumnType("REAL")
                .HasPrecision(18, 10); // Precizie mare pentru GPS

            // ========== SEEDING DATA (Date de Test) ==========
            
            // Adăugăm 3 Trasee
            modelBuilder.Entity<Models.Route>().HasData(
                new Models.Route { Id = 1, RouteNumber = "1", Name = "Cimitir - Hornbach" },
                new Models.Route { Id = 2, RouteNumber = "11", Name = "Kaufland Arhitecților - Continental" },
                new Models.Route { Id = 3, RouteNumber = "2", Name = "Scandia/Neveon - Piața Cibin" }
            );

            // Adăugăm stațiile reale pentru Linia 1: Cimitir - Hornbach (17 stații)
            modelBuilder.Entity<Station>().HasData(
                new Station { Id = 1, Name = "Cimitir", Latitude = 45.7679372, Longitude = 24.138513 },
                new Station { Id = 2, Name = "Calea Dumbrăvii (Cimitir)", Latitude = 45.7704342, Longitude = 24.138481 },
                new Station { Id = 3, Name = "Str. Siretului", Latitude = 45.7720088, Longitude = 24.1404907 },
                new Station { Id = 4, Name = "Calea Cisnădiei", Latitude = 45.7745539, Longitude = 24.1485366 },
                new Station { Id = 5, Name = "Str. Rahovei (Piața Rahova)", Latitude = 45.7773955, Longitude = 24.157935 },
                new Station { Id = 6, Name = "B-dul M. Viteazu", Latitude = 45.7791296, Longitude = 24.1556418 },
                new Station { Id = 7, Name = "Calea Dumbrăvii (Viteazu)", Latitude = 45.7817672, Longitude = 24.1497772 },
                new Station { Id = 8, Name = "Piața Unirii", Latitude = 45.7909724, Longitude = 24.1485152 },
                new Station { Id = 9, Name = "Str. A. Șaguna", Latitude = 45.7926259, Longitude = 24.1408296 },
                new Station { Id = 10, Name = "Șos. Alba Iulia", Latitude = 45.7926259, Longitude = 24.1408296 },
                new Station { Id = 11, Name = "Str. Malului", Latitude = 45.7961989, Longitude = 24.1390757 },
                new Station { Id = 12, Name = "Piața Cibin", Latitude = 45.7989894, Longitude = 24.1444539 },
                new Station { Id = 13, Name = "Str. Cibinului", Latitude = 45.799473, Longitude = 24.1442871 },
                new Station { Id = 14, Name = "Str. Rusciorului", Latitude = 45.8086417, Longitude = 24.1495909 },
                new Station { Id = 15, Name = "Str. Lungă", Latitude = 45.8074496, Longitude = 24.1458732 },
                new Station { Id = 16, Name = "Calea Șurii Mari (Halta Sibiu)", Latitude = 45.8112544, Longitude = 24.148464 },
                new Station { Id = 17, Name = "Hornbach", Latitude = 45.8210754, Longitude = 24.1491845 },
                
                // Stațiile pentru Traseul 11: Kaufland Arhitecților - Continental (13 stații)
                new Station { Id = 20, Name = "Kaufland Arhitecților", Latitude = 45.7706136, Longitude = 24.1466656 },
                new Station { Id = 21, Name = "C. Cisnădiei - Primăverii", Latitude = 45.7726737, Longitude = 24.1481128 },
                new Station { Id = 22, Name = "Cartierul Primăverii", Latitude = 45.7546876, Longitude = 24.1387102 },
                new Station { Id = 23, Name = "Str. Rahova", Latitude = 45.7773955, Longitude = 24.157935 },
                new Station { Id = 24, Name = "B-dul M. Viteazu (Iorga)", Latitude = 45.7781197, Longitude = 24.1496908 },
                new Station { Id = 25, Name = "Str. N. Iorga / Cedonia", Latitude = 45.78091, Longitude = 24.158085 },
                new Station { Id = 26, Name = "Str. Luptei", Latitude = 45.7825472, Longitude = 24.1624787 },
                new Station { Id = 27, Name = "B-dul V. Milea", Latitude = 45.7883944, Longitude = 24.1515383 },
                // Stația 8 (Piața Unirii) și 9 (Str. A. Șaguna / Șos. Alba Iulia) sunt comune cu Traseul 1
                new Station { Id = 28, Name = "Str. Salzburg", Latitude = 45.7964091, Longitude = 24.0782698 },
                new Station { Id = 29, Name = "S.C. Continental", Latitude = 45.7978947, Longitude = 24.0770893 },
                
                // Stațiile pentru Linia 2: Scandia/Neveon - Piața Cibin (13 stații)
                new Station { Id = 30, Name = "Scandia/S.C. Neveon", Latitude = 45.7650123, Longitude = 24.1123456 },
                new Station { Id = 31, Name = "IRMES", Latitude = 45.7689234, Longitude = 24.1234567 },
                new Station { Id = 32, Name = "Str. Ștefan cel Mare", Latitude = 45.7834567, Longitude = 24.1389012 },
                new Station { Id = 33, Name = "Str. Semaforului", Latitude = 45.7867890, Longitude = 24.1423456 },
                // Stația 27 (B-dul V. Milea) este comună cu Traseul 11
                // Stația 8 (Piața Unirii) este comună cu Traseul 1 și 11
                new Station { Id = 34, Name = "B-dul Coposu", Latitude = 45.7923456, Longitude = 24.1501234 },
                new Station { Id = 35, Name = "Gară CFR Sibiu", Latitude = 45.7926100, Longitude = 24.1513700 },
                new Station { Id = 36, Name = "Str. Regele Ferdinand", Latitude = 45.7945678, Longitude = 24.1534567 },
                new Station { Id = 37, Name = "Str. Abatorului", Latitude = 45.7967890, Longitude = 24.1556789 },
                new Station { Id = 38, Name = "Târgul Fânului", Latitude = 45.7989012, Longitude = 24.1578901 },
                new Station { Id = 39, Name = "Str. Râului", Latitude = 45.7998765, Longitude = 24.1456789 }
                // Stația 12 (Piața Cibin) este comună cu Traseul 1
            );

            // Adăugăm 3 Autobuze
            modelBuilder.Entity<Bus>().HasData(
                new Bus { Id = 1, LicensePlate = "SB-01-ABC", InternalName = "Bus 101", CurrentRouteId = 1 },
                new Bus { Id = 2, LicensePlate = "SB-11-XYZ", InternalName = "Bus 211", CurrentRouteId = 2 },
                new Bus { Id = 3, LicensePlate = "SB-02-DEF", InternalName = "Bus 102", CurrentRouteId = 3 }
            );

            // Definim stațiile pentru Traseul 1 (Cimitir - Hornbach) - 17 stații
            modelBuilder.Entity<RouteStation>().HasData(
                new RouteStation { Id = 1, RouteId = 1, StationId = 1, Order = 1 },   // Cimitir
                new RouteStation { Id = 2, RouteId = 1, StationId = 2, Order = 2 },   // Calea Dumbrăvii (Cimitir)
                new RouteStation { Id = 3, RouteId = 1, StationId = 3, Order = 3 },   // Str. Siretului
                new RouteStation { Id = 4, RouteId = 1, StationId = 4, Order = 4 },   // Calea Cisnădiei
                new RouteStation { Id = 5, RouteId = 1, StationId = 5, Order = 5 },   // Str. Rahovei
                new RouteStation { Id = 6, RouteId = 1, StationId = 6, Order = 6 },   // B-dul M. Viteazu
                new RouteStation { Id = 7, RouteId = 1, StationId = 7, Order = 7 },   // Calea Dumbrăvii (Viteazu)
                new RouteStation { Id = 8, RouteId = 1, StationId = 8, Order = 8 },   // Piața Unirii
                new RouteStation { Id = 9, RouteId = 1, StationId = 9, Order = 9 },   // Str. A. Șaguna
                new RouteStation { Id = 10, RouteId = 1, StationId = 10, Order = 10 }, // Șos. Alba Iulia
                new RouteStation { Id = 11, RouteId = 1, StationId = 11, Order = 11 }, // Str. Malului
                new RouteStation { Id = 12, RouteId = 1, StationId = 12, Order = 12 }, // Piața Cibin
                new RouteStation { Id = 13, RouteId = 1, StationId = 13, Order = 13 }, // Str. Cibinului
                new RouteStation { Id = 14, RouteId = 1, StationId = 14, Order = 14 }, // Str. Rusciorului
                new RouteStation { Id = 15, RouteId = 1, StationId = 15, Order = 15 }, // Str. Lungă
                new RouteStation { Id = 16, RouteId = 1, StationId = 16, Order = 16 }, // Calea Șurii Mari
                new RouteStation { Id = 17, RouteId = 1, StationId = 17, Order = 17 }  // Hornbach
            );

            // Definim stațiile pentru Traseul 11 (Kaufland Arhitecților - Continental) - 13 stații
            modelBuilder.Entity<RouteStation>().HasData(
                new RouteStation { Id = 18, RouteId = 2, StationId = 20, Order = 1 },  // Kaufland Arhitecților
                new RouteStation { Id = 19, RouteId = 2, StationId = 21, Order = 2 },  // C. Cisnădiei - Primăverii
                new RouteStation { Id = 20, RouteId = 2, StationId = 22, Order = 3 },  // Cartierul Primăverii
                new RouteStation { Id = 21, RouteId = 2, StationId = 4, Order = 4 },   // Calea Cisnădiei (comună cu Traseul 1)
                new RouteStation { Id = 22, RouteId = 2, StationId = 23, Order = 5 },  // Str. Rahova
                new RouteStation { Id = 23, RouteId = 2, StationId = 24, Order = 6 },  // B-dul M. Viteazu (Iorga)
                new RouteStation { Id = 24, RouteId = 2, StationId = 25, Order = 7 },  // Str. N. Iorga / Cedonia
                new RouteStation { Id = 25, RouteId = 2, StationId = 26, Order = 8 },  // Str. Luptei
                new RouteStation { Id = 26, RouteId = 2, StationId = 27, Order = 9 },  // B-dul V. Milea
                new RouteStation { Id = 27, RouteId = 2, StationId = 8, Order = 10 },  // Piața Unirii (comună)
                new RouteStation { Id = 28, RouteId = 2, StationId = 9, Order = 11 },  // Str. A. Șaguna (comună)
                new RouteStation { Id = 29, RouteId = 2, StationId = 28, Order = 12 }, // Str. Salzburg
                new RouteStation { Id = 30, RouteId = 2, StationId = 29, Order = 13 }  // S.C. Continental
            );

            // Definim stațiile pentru Linia 2 (Scandia/Neveon - Piața Cibin) - 13 stații
            modelBuilder.Entity<RouteStation>().HasData(
                new RouteStation { Id = 31, RouteId = 3, StationId = 30, Order = 1 },  // Scandia/S.C. Neveon
                new RouteStation { Id = 32, RouteId = 3, StationId = 31, Order = 2 },  // IRMES
                new RouteStation { Id = 33, RouteId = 3, StationId = 32, Order = 3 },  // Str. Ștefan cel Mare
                new RouteStation { Id = 34, RouteId = 3, StationId = 33, Order = 4 },  // Str. Semaforului
                new RouteStation { Id = 35, RouteId = 3, StationId = 27, Order = 5 },  // B-dul V. Milea (comună cu Linia 11)
                new RouteStation { Id = 36, RouteId = 3, StationId = 8, Order = 6 },   // Piața Unirii (comună cu Linia 1 și 11)
                new RouteStation { Id = 37, RouteId = 3, StationId = 34, Order = 7 },  // B-dul Coposu
                new RouteStation { Id = 38, RouteId = 3, StationId = 35, Order = 8 },  // Gară CFR Sibiu
                new RouteStation { Id = 39, RouteId = 3, StationId = 36, Order = 9 },  // Str. Regele Ferdinand
                new RouteStation { Id = 40, RouteId = 3, StationId = 37, Order = 10 }, // Str. Abatorului
                new RouteStation { Id = 41, RouteId = 3, StationId = 38, Order = 11 }, // Târgul Fânului
                new RouteStation { Id = 42, RouteId = 3, StationId = 39, Order = 12 }, // Str. Râului
                new RouteStation { Id = 43, RouteId = 3, StationId = 12, Order = 13 }  // Piața Cibin (comună cu Linia 1)
            );
        }
    }
}
