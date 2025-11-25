// Models/Bus.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace TursibBackend.Models
{
    // Reprezintă un autobuz fizic
    public class Bus
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; } // Număr înmatriculare
        public string InternalName { get; set; } // Ex: "Bus 045"

        // Relație: Traseul pe care este asignat autobuzul ACUM
        // Poate fi 'null' dacă autobuzul este în garaj.
        public int? CurrentRouteId { get; set; }
        
        [ForeignKey("CurrentRouteId")]
        public Route CurrentRoute { get; set; }
    }
}
