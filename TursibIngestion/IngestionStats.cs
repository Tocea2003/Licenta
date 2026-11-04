using System.Collections.Concurrent;

namespace TursibIngestion;

/// <summary>Thread-safe counters exposed on /health and logged periodically.</summary>
public sealed class IngestionStats
{
    private long received;
    private long accepted;
    private long rejected;
    private long produceErrors;

    public ConcurrentDictionary<string, long> RejectReasons { get; } = new();

    public bool MqttConnected { get; set; }
    public DateTimeOffset? LastMessageAt { get; private set; }

    public long Received => Interlocked.Read(ref received);
    public long Accepted => Interlocked.Read(ref accepted);
    public long Rejected => Interlocked.Read(ref rejected);
    public long ProduceErrors => Interlocked.Read(ref produceErrors);

    public void OnReceived()
    {
        Interlocked.Increment(ref received);
        LastMessageAt = DateTimeOffset.UtcNow;
    }

    public void OnAccepted() => Interlocked.Increment(ref accepted);

    public void OnRejected(string reason)
    {
        Interlocked.Increment(ref rejected);
        RejectReasons.AddOrUpdate(reason, 1, (_, n) => n + 1);
    }

    public void OnProduceError() => Interlocked.Increment(ref produceErrors);
}
