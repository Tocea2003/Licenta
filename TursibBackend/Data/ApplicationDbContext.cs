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
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<ServiceCalendar> ServiceCalendars { get; set; }

        // Tabele GTFS (populate de GTFSImporter)
        public DbSet<Trip> Trips { get; set; }
        public DbSet<StopTime> StopTimes { get; set; }
        public DbSet<ShapePoint> ShapePoints { get; set; }

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

            // Coordonatele GPS sunt double -> "double precision" (8 bytes) în PostgreSQL

            // Precision pentru preturi
            modelBuilder.Entity<Ticket>()
                .Property(t => t.PriceRon)
                .HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(10,2)");

            // Ticket -> Payment (optional)
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Payment)
                .WithMany()
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Ticket -> User
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique index on Username for fast lookup on every auth/favorites call
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // ServiceCalendar - index pe ServiceId pentru join rapid cu Trips
            modelBuilder.Entity<ServiceCalendar>()
                .HasIndex(sc => sc.ServiceId);

            // ========== TABELE GTFS ==========
            modelBuilder.Entity<Trip>(e =>
            {
                e.HasIndex(t => t.TripId);
                e.HasIndex(t => t.RouteId);
                e.HasOne<Models.Route>().WithMany().HasForeignKey(t => t.RouteId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StopTime>(e =>
            {
                e.HasIndex(st => new { st.TripId, st.StopSequence });
                e.HasIndex(st => st.StopId);
                e.HasOne<Station>().WithMany().HasForeignKey(st => st.StopId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ShapePoint>(e =>
            {
                e.ToTable("Shapes");
                e.HasIndex(p => new { p.ShapeId, p.Sequence });
            });
        }
    }
}
