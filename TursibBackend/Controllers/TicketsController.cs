using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;
using TursibBackend.Services;

namespace TursibBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PaymentSimulatorService _payments;
        private readonly ILogger<TicketsController> _logger;

        // Catalog of Tursib Sibiu ticket types (tarife în vigoare de la 1 aprilie 2025)
        private static readonly Dictionary<string, (decimal Price, int ValidityMinutes, int? RidesTotal)> TicketCatalog = new()
        {
            ["single"]           = (3.50m,  60,           null),   // Bilet intern 60 min
            ["daily"]            = (7.00m,  60 * 24,      null),   // Legitimație zilnică internă
            ["weekly"]           = (24.00m, 60 * 24 * 7,  null),   // Abonament 7 zile intern
            ["monthly_nominal"]  = (90.00m, 60 * 24 * 30, null),   // Abonament nominal 30 zile
            ["monthly_nonnominal"] = (126.00m, 60 * 24 * 30, null), // Abonament nenominal 30 zile
        };

        public TicketsController(ApplicationDbContext context, PaymentSimulatorService payments, ILogger<TicketsController> logger)
        {
            _context = context;
            _payments = payments;
            _logger = logger;
        }

        // POST: api/Tickets/purchase
        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase([FromBody] PurchaseTicketRequest request)
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "Token invalid sau lipsa" });

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return NotFound(new { message = "Utilizator inexistent" });

            if (string.IsNullOrWhiteSpace(request.CardholderName))
                return BadRequest(new { message = "Numele detinatorului cardului este obligatoriu" });

            if (string.IsNullOrWhiteSpace(request.CardNumber))
                return BadRequest(new { message = "Numarul cardului este obligatoriu" });

            if (string.IsNullOrWhiteSpace(request.ExpiryMonth) || string.IsNullOrWhiteSpace(request.ExpiryYear))
                return BadRequest(new { message = "Data expirarii cardului este obligatorie" });

            if (string.IsNullOrWhiteSpace(request.Cvv))
                return BadRequest(new { message = "CVV-ul este obligatoriu" });

            // Resolve ticket type from catalog
            var ticketType = (request.TicketType ?? "single").ToLower();
            if (!TicketCatalog.TryGetValue(ticketType, out var catalog))
                return BadRequest(new { message = $"Tip bilet necunoscut: {ticketType}" });

            var price = catalog.Price;
            var validFrom = DateTime.UtcNow;
            var validUntil = validFrom.AddMinutes(catalog.ValidityMinutes);

            var digits = PaymentSimulatorService.Normalize(request.CardNumber);
            var brand = _payments.DetectBrand(request.CardNumber);
            var last4 = digits.Length >= 4 ? digits.Substring(digits.Length - 4) : digits;

            var (ok, reason) = _payments.Simulate(request.CardNumber, request.ExpiryMonth, request.ExpiryYear, request.Cvv);

            if (!ok)
            {
                var failed = new Payment
                {
                    UserId = user.Id,
                    Amount = price,
                    CardLast4 = last4,
                    CardBrand = brand,
                    CardholderName = request.CardholderName,
                    Status = "failed",
                    FailureReason = reason
                };
                _context.Payments.Add(failed);
                await _context.SaveChangesAsync();

                return StatusCode(StatusCodes.Status402PaymentRequired, new { message = reason ?? "Plata esuata" });
            }

            var payment = new Payment
            {
                UserId = user.Id,
                Amount = price,
                CardLast4 = last4,
                CardBrand = brand,
                CardholderName = request.CardholderName,
                Status = "succeeded"
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var ticket = new Ticket
            {
                UserId = user.Id,
                TicketType = ticketType,
                PriceRon = price,
                Status = "active",
                PurchasedAt = DateTime.UtcNow,
                ValidFrom = validFrom,
                ValidUntil = validUntil,
                QrToken = Guid.NewGuid().ToString("N"),
                PaymentId = payment.Id
            };
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ToDto(ticket, payment));
        }

        // GET: api/Tickets
        [HttpGet]
        public async Task<IActionResult> GetMyTickets()
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "Token invalid sau lipsa" });

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return NotFound(new { message = "Utilizator inexistent" });

            var tickets = await _context.Tickets
                .Where(t => t.UserId == user.Id)
                .Include(t => t.Payment)
                .OrderByDescending(t => t.PurchasedAt)
                .ToListAsync();

            return Ok(tickets.Select(t => ToDto(t, t.Payment)));
        }

        // GET: api/Tickets/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTicket(int id)
        {
            var username = GetUsernameFromToken();
            if (string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "Token invalid sau lipsa" });

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return NotFound(new { message = "Utilizator inexistent" });

            var ticket = await _context.Tickets
                .Include(t => t.Payment)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null || ticket.UserId != user.Id)
                return NotFound(new { message = "Bilet inexistent" });

            return Ok(ToDto(ticket, ticket.Payment));
        }

        // Effective status: a ticket past its validity is "expired" even if the stored
        // value is still "active" (the DB field is never auto-updated on expiry).
        private static string EffectiveStatus(Ticket t) =>
            t.Status == "used"
                ? "used"
                : (DateTime.UtcNow >= t.ValidUntil ? "expired" : t.Status);

        private static object ToDto(Ticket t, Payment? p) => new
        {
            id = t.Id,
            ticketType = t.TicketType,
            priceRon = t.PriceRon,
            status = EffectiveStatus(t),
            purchasedAt = t.PurchasedAt,
            validFrom = t.ValidFrom,
            validUntil = t.ValidUntil,
            qrToken = t.QrToken,
            ridesTotal = TicketCatalog.TryGetValue(t.TicketType ?? "single", out var cat) ? cat.RidesTotal : null,
            payment = p == null ? null : new
            {
                id = p.Id,
                amount = p.Amount,
                currency = p.Currency,
                cardLast4 = p.CardLast4,
                cardBrand = p.CardBrand,
                status = p.Status
            }
        };

        private string? GetUsernameFromToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader.Substring("Bearer ".Length).Trim();
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var claim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == System.Security.Claims.ClaimTypes.Name ||
                    c.Type == "unique_name" ||
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
                return claim?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JWT parsing failed: {Message}", ex.Message);
                return null;
            }
        }
    }
}
