namespace TursibBackend.Models
{
    /// <summary>
    /// O poziție salvată pentru replay (tabelul "VehiclePositionHistory"). Se păstrează un eșantion per autobuz,
    /// nu fiecare mesaj, ca volumul să rămână mic (vezi <c>History</c> în configurare).
    /// </summary>
    public class VehiclePositionRecord
    {
        public long Id { get; set; }
        public string VehicleId { get; set; } = string.Empty;
        public int RouteId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; }
        public int? Occupancy { get; set; }
        public string? Status { get; set; }
        public int? DirectionId { get; set; }
        /// <summary>Momentul poziției (UTC).</summary>
        public DateTime RecordedAt { get; set; }
    }
}
