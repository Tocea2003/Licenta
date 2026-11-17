using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TursibBackend.Services;

namespace TursibBackend.Controllers;

/// <summary>
/// Starea platformei live, pentru verificarea unei implementări: sosesc poziții? e conectată ingestia MQTT/Kafka?
/// se scrie istoricul? există orar azi (pentru planificator și trip_id)? Nu expune adrese, utilizatori sau parole.
/// </summary>
[Route("api/live/status")]
[ApiController]
[EnableRateLimiting("general")]
public class StatusController : ControllerBase
{
    private const int FreshSeconds = 60;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] LiveBusState state, [FromServices] LiveIngestService ingest, [FromServices] TripMatcher trips,
        [FromServices] HistoryOptions history, [FromServices] IConfiguration config, [FromServices] TimeProvider time,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var snapshot = state.Snapshot();
        var embedded = HttpContext.RequestServices.GetService<EmbeddedMqttIngest>();
        var kafka = HttpContext.RequestServices.GetService<KafkaPositionConsumer>();
        var historyWriter = HttpContext.RequestServices.GetService<PositionHistoryWriter>();
        var historyBuffer = HttpContext.RequestServices.GetService<PositionHistoryBuffer>();

        object schedule;
        try
        {
            var (date, count) = await trips.ScheduleSummaryAsync(now, ct);
            schedule = new { serviceDate = date.ToString("yyyy-MM-dd"), tripsToday = count ?? 0, hasCalendar = count != null };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            schedule = new { serviceDate = (string?)null, tripsToday = 0, hasCalendar = false, error = ex.GetType().Name };
        }

        return Ok(new
        {
            time = now.ToUnixTimeSeconds(),
            live = new
            {
                buses = snapshot.Count,
                fresh = snapshot.Count(p => p.Timestamp >= now.ToUnixTimeSeconds() - FreshSeconds),
                acceptedTotal = ingest.AcceptedTotal,
                lastIngestAt = ingest.LastIngestAt
            },
            ingest = new
            {
                http = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INGEST_API_KEY") ?? config["Ingest:ApiKey"]),
                embeddedMqtt = embedded == null ? null : new { connected = embedded.Connected, received = embedded.Received, rejected = embedded.Rejected },
                kafka = kafka == null ? null : new { lastMessageAt = kafka.LastMessageAt }
            },
            history = new
            {
                enabled = history.Enabled,
                buffered = historyBuffer?.Count ?? 0,
                written = historyWriter?.Written ?? 0,
                lastFlushAt = historyWriter?.LastFlushAt,
                lastError = historyWriter?.LastError
            },
            schedule
        });
    }
}
