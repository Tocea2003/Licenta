namespace TursibBackend.Models
{
    /// <summary>
    /// O cursă GTFS (trips.txt): o plecare concretă pe un traseu, într-o direcție.
    /// </summary>
    public class Trip
    {
        public int Id { get; set; }
        public string TripId { get; set; } = string.Empty;
        public int RouteId { get; set; }
        public string? ServiceId { get; set; }
        public string? TripHeadsign { get; set; }
        public int? DirectionId { get; set; }
        public string? ShapeId { get; set; }
    }
}
