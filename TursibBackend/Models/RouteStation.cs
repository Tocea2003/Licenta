// Models/RouteStation.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace TursibBackend.Models
{
    // Aceasta este "inima" logicii.
    // Leagă un Traseu de o Stație și specifică ordinea.
    public class RouteStation
    {
        public int Id { get; set; }

        // Cheia străină pentru Traseu
        public int RouteId { get; set; }
        [ForeignKey("RouteId")]
        public Route Route { get; set; }

        // Cheia străină pentru Stație
        public int StationId { get; set; }
        [ForeignKey("StationId")]
        public Station Station { get; set; }

        // Câmpul crucial: ordinea stației pe traseu (ex: 1, 2, 3...)
        public int Order { get; set; }
    }
}
