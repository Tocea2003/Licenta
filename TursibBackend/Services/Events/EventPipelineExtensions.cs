using System.Text.Json.Serialization;

namespace TursibBackend.Services.Events;

public static class EventPipelineExtensions
{
    /// <summary>
    /// Registers the Kafka consumer, SignalR broadcaster and history writer.
    /// Returns false (and registers nothing) when Kafka:BootstrapServers is empty.
    /// </summary>
    public static bool AddEventPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EventPipelineOptions.Section);
        var options = section.Get<EventPipelineOptions>() ?? new EventPipelineOptions();
        if (!options.Enabled)
            return false;

        services.Configure<EventPipelineOptions>(section);
        services.AddSignalR().AddJsonProtocol(o =>
            o.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        services.AddSingleton(new HistorySampler(TimeSpan.FromSeconds(options.HistorySampleSeconds)));
        services.AddSingleton<HistoryQueue>();
        services.AddHostedService<VehiclePositionConsumer>();
        services.AddHostedService<LiveBroadcastService>();
        services.AddHostedService<HistoryWriter>();
        return true;
    }
}
