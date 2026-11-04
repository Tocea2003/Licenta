namespace TursibBackend.Models
{
    /// <summary>
    /// Un punct din geometria unui traseu (shapes.txt). Stocat în tabelul "Shapes".
    /// </summary>
    public class ShapePoint
    {
        public int Id { get; set; }
        public string ShapeId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Sequence { get; set; }
    }
}
