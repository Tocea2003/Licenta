namespace TursibBackend.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RON";
        public string CardLast4 { get; set; } = string.Empty;
        public string CardBrand { get; set; } = string.Empty; // visa | mastercard | unknown
        public string CardholderName { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending | succeeded | failed
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
