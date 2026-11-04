using System.Collections.Concurrent;
using Tursib.Contracts;

namespace TursibBackend.Services.Events;

/// <summary>
/// Keeps at most one history row per vehicle per interval: ~35 rows/s for the
/// whole fleet instead of ~340, which is plenty for replay.
/// </summary>
public sealed class HistorySampler
{
    private readonly ConcurrentDictionary<string, long> lastRecorded = new();
    private readonly long intervalSeconds;

    public HistorySampler(TimeSpan interval) => intervalSeconds = Math.Max(1, (long)interval.TotalSeconds);

    public bool ShouldRecord(VehiclePosition position)
    {
        var recorded = false;
        lastRecorded.AddOrUpdate(
            position.VehicleId,
            _ => { recorded = true; return position.Timestamp; },
            (_, last) =>
            {
                if (position.Timestamp - last >= intervalSeconds)
                {
                    recorded = true;
                    return position.Timestamp;
                }
                recorded = false;
                return last;
            });
        return recorded;
    }
}
