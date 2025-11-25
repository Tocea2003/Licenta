// Models/Route.cs
using System.Collections.Generic;

namespace TursibBackend.Models
{
    // Reprezintă un traseu (ex: "Traseul 5", "Valea Aurie -> Gara")
    public class Route
    {
        public int Id { get; set; }
        public string RouteNumber { get; set; } // Ex: "5", "T1", "22"
        public string Name { get; set; } // Ex: "Valea Aurie -> Gara"
        public string? Color { get; set; } // Ex: "#FF0000" - culoarea traseului din GTFS

        // Relație: Un traseu are o listă de opriri (stații în ordine)
        // Aceasta este colecția de legături din tabelul RouteStation
        public ICollection<RouteStation> RouteStations { get; set; }
    }
}
