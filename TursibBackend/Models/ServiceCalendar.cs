namespace TursibBackend.Models
{
    /// <summary>
    /// Model pentru datele din GTFS calendar.txt.
    /// Definește în ce zile ale săptămânii circulă un anumit service_id.
    /// </summary>
    public class ServiceCalendar
    {
        public int Id { get; set; }
        public string ServiceId { get; set; } = "";
        public bool Monday { get; set; }
        public bool Tuesday { get; set; }
        public bool Wednesday { get; set; }
        public bool Thursday { get; set; }
        public bool Friday { get; set; }
        public bool Saturday { get; set; }
        public bool Sunday { get; set; }
        public string StartDate { get; set; } = "";  // Format: yyyy-MM-dd
        public string EndDate { get; set; } = "";     // Format: yyyy-MM-dd
    }
}
