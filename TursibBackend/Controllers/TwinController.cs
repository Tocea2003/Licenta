using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TursibBackend.Services;

namespace TursibBackend.Controllers;

[Route("api/twin")]
[ApiController]
[EnableRateLimiting("general")]
public class TwinController : ControllerBase
{
    private readonly TwinService _twin;
    public TwinController(TwinService twin) => _twin = twin;

    /// <summary>
    /// Rulează un scenariu în digital twin al liniei (simulare accelerată, deterministă pentru același seed)
    /// și întoarce metricile de bunching. Nu afectează datele live.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<TwinRunResult>> Run([FromBody] TwinScenario scenario, CancellationToken ct)
    {
        var error = scenario.Validate();
        if (error != null) return BadRequest(new { message = error });

        var result = await _twin.RunAsync(scenario, ct);
        return result == null
            ? NotFound(new { message = $"Linia {scenario.RouteId} nu are geometrie (shape), nu o putem simula." })
            : Ok(result);
    }
}
