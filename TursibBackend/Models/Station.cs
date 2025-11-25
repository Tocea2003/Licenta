// Models/Station.cs
using System.Collections.Generic;

namespace TursibBackend.Models
{
    // Reprezintă o stație fizică, cu locația ei
    public class Station
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Relație: O stație poate aparține mai multor trasee
        // Aceasta este colecția de legături din tabelul RouteStation
        public ICollection<RouteStation> RouteStations { get; set; }
    }
}
