using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;
using TursibBackend.Services;
using TursibIngestion;
using TursibMessaging;
using Xunit;

namespace TursibBackend.Tests;

/// <summary>Ascultă sursa „Tursib.Pipeline” ca un exportator OpenTelemetry, dar în memorie.</summary>
public sealed class PipelineListener : IDisposable
{
    private readonly ActivityListener _activities;
    private readonly MeterListener _meters = new();
    public ConcurrentQueue<Activity> Activities { get; } = new();
    public ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object?> Tags)> Measurements { get; } = new();

    public PipelineListener()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = s => s.Name == PipelineTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => Activities.Enqueue(a)
        };
        ActivitySource.AddActivityListener(_activities);

        _meters.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == PipelineTelemetry.Name) listener.EnableMeasurementEvents(instrument);
        };
        _meters.SetMeasurementEventCallback<long>((i, v, tags, _) => Record(i, v, tags));
        _meters.SetMeasurementEventCallback<double>((i, v, tags, _) => Record(i, v, tags));
        _meters.Start();
    }

    private void Record(Instrument i, double v, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var d = new Dictionary<string, object?>();
        foreach (var t in tags) d[t.Key] = t.Value;
        Measurements.Enqueue((i.Name, v, d));
    }

    public void Dispose()
    {
        _activities.Dispose();
        _meters.Dispose();
    }
}

[Collection("telemetry")]
public class TelemetryTests
{
    private sealed class NullSink : IPositionSink
    {
        public Task PublishAsync(string vehicleId, string json, CancellationToken ct) => Task.CompletedTask;
        public Task PublishRejectedAsync(string topic, ReadOnlyMemory<byte> payload, RejectReason reason, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Ingestion_TracesEachMessage_AndCountsAcceptedAndRejected()
    {
        using var listener = new PipelineListener();
        var ingestor = new PositionIngestor(new NullSink(), new IngestionMetrics(), NullLogger<PositionIngestor>.Instance);
        var vehicle = $"route_31_bus_{Guid.NewGuid():N}"[..20];
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 4;

        await ingestor.HandleAsync($"tursib/vehicles/{vehicle}/position",
            Encoding.UTF8.GetBytes($$"""{"latitude":45.79,"longitude":24.15,"routeId":31,"timestamp":{{ts}}}"""), default);
        await ingestor.HandleAsync($"tursib/vehicles/{vehicle}/position", Encoding.UTF8.GetBytes("garbage"), default);

        var spans = listener.Activities.Where(a => a.OperationName == "mqtt receive position"
                                                   && (string?)a.GetTagItem("messaging.destination.name") == $"tursib/vehicles/{vehicle}/position").ToList();
        Assert.Equal(2, spans.Count);
        var ok = Assert.Single(spans, s => s.Status != ActivityStatusCode.Error);
        Assert.Equal(vehicle, ok.GetTagItem("tursib.vehicle_id"));
        Assert.Equal(ActivityKind.Consumer, ok.Kind);
        var bad = Assert.Single(spans, s => s.Status == ActivityStatusCode.Error);
        Assert.Equal("InvalidJson", bad.GetTagItem("tursib.reject_reason"));

        Assert.Contains(listener.Measurements, m => m.Instrument == "tursib.positions.rejected" && (string?)m.Tags["reason"] == "InvalidJson");
        Assert.Contains(listener.Measurements, m => m.Instrument == "tursib.positions.accepted" && (string?)m.Tags["stage"] == "ingestion");
        Assert.Contains(listener.Measurements, m => m.Instrument == "tursib.positions.age" && m.Value >= 4);
    }

    [Fact]
    public void TraceContext_RoundTripsThroughMessageHeaders()
    {
        using var listener = new PipelineListener();
        using var producer = PipelineTelemetry.Source.StartActivity("test produce")!;
        var headers = new Dictionary<string, string>();
        PipelineTelemetry.Inject(producer, (k, v) => headers[k] = v);

        var context = PipelineTelemetry.Extract(k => headers.GetValueOrDefault(k));

        Assert.Equal(producer.TraceId, context.TraceId);
        Assert.Equal(producer.SpanId, context.SpanId);
        Assert.Equal(default, PipelineTelemetry.Extract(_ => null));
        Assert.Equal(default, PipelineTelemetry.Extract(k => k == "traceparent" ? "not-a-trace" : null));
    }

    private sealed class RecordingHandler : IBusPositionHandler
    {
        public TaskCompletionSource<ActivityContext> Seen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct)
        {
            Seen.TrySetResult(Activity.Current?.Context ?? default);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task OneTrace_ContinuesFromIngestThroughTheInternalBusToEveryHandler()
    {
        using var listener = new PipelineListener();
        var bus = new LiveEventBus();
        var handler = new RecordingHandler();
        var dispatcher = new LiveEventDispatcher(bus, new[] { handler }, NullLogger<LiveEventDispatcher>.Instance);
        await dispatcher.StartAsync(default);
        try
        {
            var ingest = new LiveIngestService(new LiveBusState(), bus);
            ActivityTraceId traceId;
            using (var root = PipelineTelemetry.Source.StartActivity("kafka consume test")!)
            {
                traceId = root.TraceId;
                ingest.Ingest(new[] { new BusPosition { Id = "route_32_bus_0", RouteId = 32, Latitude = 45.7, Longitude = 24.1, Timestamp = 1 } }, "kafka");
            }

            var handled = await handler.Seen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(traceId, handled.TraceId);                               // același trace, peste canalul asincron
            Assert.Contains(listener.Activities, a => a.OperationName == "live ingest" && a.TraceId == traceId
                                                      && (string?)a.GetTagItem("tursib.transport") == "kafka");
        }
        finally { await dispatcher.StopAsync(default); }
    }

    private static ServiceProvider Services(string? endpoint)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTursibTelemetry(config, "tursib-test");
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Export_IsOnlyEnabledWhenAnOtlpEndpointIsConfigured()
    {
        using var off = Services(null);
        Assert.Null(off.GetService<TracerProvider>());

        using var on = Services("http://localhost:4317");
        Assert.NotNull(on.GetService<TracerProvider>());
    }
}
