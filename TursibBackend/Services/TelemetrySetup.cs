using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TursibMessaging;

namespace TursibBackend.Services;

public static class TelemetrySetup
{
    /// <summary>
    /// OpenTelemetry pentru backend: sursa „Tursib.Pipeline” (urme + metrici), ASP.NET Core, HttpClient și runtime.
    /// Exportul OTLP e activ doar dacă e setat <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (standardul OpenTelemetry).
    /// </summary>
    public static IServiceCollection AddTursibTelemetry(this IServiceCollection services, IConfiguration config, string serviceName)
    {
        var endpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint)) return services;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: typeof(TelemetrySetup).Assembly.GetName().Version?.ToString()))
            .WithTracing(t => t
                .AddSource(PipelineTelemetry.Name)
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/hubs"))
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddMeter(PipelineTelemetry.Name, LiveTelemetry.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());
        return services;
    }
}

/// <summary>Metrici de stare ale platformei live (gauge-uri citite la fiecare export).</summary>
public sealed class LiveTelemetry : IDisposable
{
    public const string MeterName = "Tursib.Live";
    private readonly System.Diagnostics.Metrics.Meter _meter = new(MeterName, "1.0");

    public LiveTelemetry(LiveBusState state, BunchingDetector bunching)
    {
        _meter.CreateObservableGauge("tursib.live.buses", () => state.Snapshot().Count, "{bus}", "Autobuze în starea live");
        _meter.CreateObservableGauge("tursib.live.buses.fresh", () =>
        {
            var oldest = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60;
            return state.Snapshot().Count(p => p.Timestamp >= oldest);
        }, "{bus}", "Autobuze care au raportat în ultimul minut");
        _meter.CreateObservableGauge("tursib.bunching.active", () => bunching.Active.Count, "{alert}", "Alerte de bunching active");
        _meter.CreateObservableGauge("tursib.bunching.critical", () => bunching.Active.Count(a => a.Severity == "critical"),
            "{alert}", "Alerte de bunching critice");
    }

    public void Dispose() => _meter.Dispose();
}
