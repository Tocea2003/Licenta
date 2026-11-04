namespace TursibBackend.Models
{
    /// <summary>
    /// GTFS calendar_dates.txt: o excepție de la calendarul săptămânal pentru o zi anume
    /// (de ex. sărbători legale sau vacanțe).
    /// </summary>
    public class ServiceCalendarDate
    {
        /// <summary>exception_type = 1: serviciul circulă suplimentar în acea zi.</summary>
        public const int ServiceAdded = 1;
        /// <summary>exception_type = 2: serviciul nu circulă în acea zi.</summary>
        public const int ServiceRemoved = 2;

        public int Id { get; set; }
        public string ServiceId { get; set; } = "";
        public string Date { get; set; } = "";  // Format: yyyy-MM-dd
        public int ExceptionType { get; set; }
    }
}
