namespace TursibBackend.Services.Events;

/// <summary>Configuration section "Kafka" (env: Kafka__BootstrapServers, ...).</summary>
public sealed class EventPipelineOptions
{
    public const string Section = "Kafka";

    /// <summary>Empty = the event pipeline is off (production on Render keeps Firebase).</summary>
    public string? BootstrapServers { get; set; }
    public string GroupId { get; set; } = "tursib-backend";
    public int HistorySampleSeconds { get; set; } = 5;
    public int HistoryRetentionDays { get; set; } = 3;
    /// <summary>Vehicles that stop reporting disappear from the live view after this.</summary>
    public int StaleAfterSeconds { get; set; } = 120;
    public int BroadcastIntervalMs { get; set; } = 500;

    public bool Enabled => !string.IsNullOrWhiteSpace(BootstrapServers);
}
