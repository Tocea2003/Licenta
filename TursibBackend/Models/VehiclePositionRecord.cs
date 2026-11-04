namespace TursibBackend.Models
{
    /// <summary>
    /// One sampled live position (table VehiclePositionHistory), used for replay
    /// and later for headway / bus-bunching analysis.
    /// </summary>
    public class VehiclePositionRecord
    {
        public long Id { get; set; }
        public string VehicleId { get; set; } = "";
        public int RouteId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        /// <summary>km/h</summary>
        public double Speed { get; set; }
        public double Heading { get; set; }
        public int? Occupancy { get; set; }
        /// <summary>UTC time the device measured the position.</summary>
        public DateTime RecordedAt { get; set; }
    }
}
