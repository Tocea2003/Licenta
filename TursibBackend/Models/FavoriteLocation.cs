namespace TursibBackend.Models
{
    public class FavoriteLocation
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Type { get; set; } = "custom"; // "home", "work", "custom"
        public string Icon { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
    }
}
