namespace TursibBackend.Models
{
    /// <summary>
    /// O oprire a unei curse într-o stație (stop_times.txt).
    /// Orele sunt în format GTFS "HH:MM:SS" și pot depăși 24:00:00.
    /// </summary>
    public class StopTime
    {
        public int Id { get; set; }
        public string TripId { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public int StopId { get; set; }
        public int StopSequence { get; set; }
    }
}
