namespace TursibBackend.Models
{
    public class PurchaseTicketRequest
    {
        public string TicketType { get; set; } = "single";
        public string CardNumber { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty;
        public string ExpiryYear { get; set; } = string.Empty;
        public string Cvv { get; set; } = string.Empty;
        public string CardholderName { get; set; } = string.Empty;
    }
}
