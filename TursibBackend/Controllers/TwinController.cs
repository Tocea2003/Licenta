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

    /// <summary>
    /// Compară strategiile de control (fără control, headway, equalize) pe același scenariu, mediind mai multe seed-uri.
    /// Răspunsul are câte un rezumat pe strategie: timp „lipit”, neuniformitate, cost (secunde de holding pe autobuz și oră).
    /// </summary>
    [HttpPost("compare")]
    public async Task<ActionResult<TwinComparison>> Compare([FromBody] TwinCompareRequest request, CancellationToken ct)
    {
        var error = request.Validate();
        if (error != null) return BadRequest(new { message = error });

        var result = await _twin.CompareAsync(request, ct);
        return result == null
            ? NotFound(new { message = $"Linia {request.RouteId} nu are geometrie (shape), nu o putem simula." })
            : Ok(result);
    }
}
