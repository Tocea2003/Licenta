namespace TursibBackend.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string TicketType { get; set; } = "single";
        public decimal PriceRon { get; set; } = 3.00m;
        public string Status { get; set; } = "active"; // active | used | expired
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
        public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
        public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddMinutes(60);
        public string QrToken { get; set; } = string.Empty;
        public int? PaymentId { get; set; }
        public Payment? Payment { get; set; }
    }
}
