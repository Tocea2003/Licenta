using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TursibBackend.Services;

namespace TursibBackend.Controllers;

[Route("api/live")]
[ApiController]
public class LiveController : ControllerBase
{
    public const string ApiKeyHeader = "X-Ingest-Key";

    private readonly LiveBusState _state;
    private readonly LiveEventBus _bus;
    private readonly IConfiguration _config;
    private readonly BunchingDetector _bunching;

    public LiveController(LiveBusState state, LiveEventBus bus, IConfiguration config, BunchingDetector bunching)
    {
        _state = state;
        _bus = bus;
        _config = config;
        _bunching = bunching;
    }

    /// <summary>Alertele de bunching: cele active acum și cele încheiate recent.</summary>
    [HttpGet("bunching")]
    public IActionResult GetBunching() => Ok(new { active = _bunching.Active, recent = _bunching.Recent });

    /// <summary>Ultima poziție cunoscută a tuturor autobuzelor (același conținut ca snapshot-ul SignalR).</summary>
    [HttpGet("positions")]
    public ActionResult<IReadOnlyList<BusPosition>> GetPositions() => Ok(_state.Snapshot());

    /// <summary>Ingest: simulatorul (sau un vehicul real) trimite un lot de poziții. Necesită cheia de ingest.</summary>
    [HttpPost("positions")]
    public IActionResult Ingest([FromBody] List<BusPosition> batch)
    {
        var expected = Environment.GetEnvironmentVariable("INGEST_API_KEY") ?? _config["Ingest:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Ingest nu este configurat (INGEST_API_KEY)." });

        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var provided) || !KeysEqual(provided.ToString(), expected))
            return Unauthorized(new { message = "Cheie de ingest invalidă." });

        if (batch.Count > LiveBusState.MaxBatchSize)
            return BadRequest(new { message = $"Maximum {LiveBusState.MaxBatchSize} poziții per cerere." });

        var accepted = _state.Apply(batch);
        _bus.Publish(accepted);
        return Ok(new { received = batch.Count, accepted = accepted.Count });
    }

    private static bool KeysEqual(string a, string b)
    {
        var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }
}
