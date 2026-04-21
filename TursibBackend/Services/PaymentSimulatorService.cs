namespace TursibBackend.Services
{
    public class PaymentSimulatorService
    {
        public static string Normalize(string cardNumber)
            => new string((cardNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        public bool IsValidLuhn(string cardNumber)
        {
            var digits = Normalize(cardNumber);
            if (digits.Length < 13 || digits.Length > 19) return false;

            int sum = 0;
            bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int n = digits[i] - '0';
                if (alt)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
                alt = !alt;
            }
            return sum % 10 == 0;
        }

        public string DetectBrand(string cardNumber)
        {
            var d = Normalize(cardNumber);
            if (d.Length == 0) return "unknown";
            if (d[0] == '4') return "visa";
            if (d.Length >= 2)
            {
                var p2 = int.Parse(d.Substring(0, 2));
                if (p2 >= 51 && p2 <= 55) return "mastercard";
            }
            if (d.Length >= 4)
            {
                var p4 = int.Parse(d.Substring(0, 4));
                if (p4 >= 2221 && p4 <= 2720) return "mastercard";
            }
            return "unknown";
        }

        public (bool ok, string? reason) Simulate(string cardNumber, string expMonth, string expYear, string cvv)
        {
            var digits = Normalize(cardNumber);
            if (digits.Length < 13 || digits.Length > 19)
                return (false, "Numar card invalid");

            if (!int.TryParse(expMonth, out var m) || m < 1 || m > 12)
                return (false, "Luna expirare invalida");

            if (!int.TryParse(expYear, out var y) || y < 2000 || y > 2099)
                return (false, "An expirare invalid");

            var lastDayOfMonth = new DateTime(y, m, DateTime.DaysInMonth(y, m), 23, 59, 59);
            if (lastDayOfMonth < DateTime.UtcNow)
                return (false, "Card expirat");

            if (string.IsNullOrWhiteSpace(cvv) || cvv.Length < 3 || cvv.Length > 4 || !cvv.All(char.IsDigit))
                return (false, "CVV invalid");

            // Test cards cu comportament specific
            if (digits == "4000000000000002")
                return (false, "Card refuzat");
            if (digits == "4000000000009995")
                return (false, "Fonduri insuficiente");

            if (!IsValidLuhn(cardNumber))
                return (false, "Numar card invalid (Luhn)");

            return (true, null);
        }
    }
}
