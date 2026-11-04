using System.Collections.Concurrent;
using Tursib.Contracts;

namespace TursibBackend.Services.Events;

/// <summary>
/// Latest known position of every vehicle, fed by the Kafka consumer.
/// Read by the SignalR hub (snapshot), the broadcaster (changes) and the GTFS-RT feed.
/// </summary>
public sealed class LiveVehicleState
{
    private readonly ConcurrentDictionary<string, VehiclePosition> latest = new();
    private readonly ConcurrentDictionary<string, VehiclePosition> changed = new();

    public int Count => latest.Count;

    /// <summary>
    /// Stores the position unless an equally new or newer one is already known
    /// (Kafka keeps per-vehicle order, but redeliveries can repeat old messages).
    /// Returns true when the state changed.
    /// </summary>
    public bool Upsert(VehiclePosition position)
    {
        var stored = latest.AddOrUpdate(
            position.VehicleId,
            position,
            (_, current) => position.Timestamp >= current.Timestamp ? position : current);

        if (!ReferenceEquals(stored, position))
            return false;

        changed[position.VehicleId] = position;
        return true;
    }

    public IReadOnlyList<VehiclePosition> Snapshot() => latest.Values.ToList();

    /// <summary>Positions changed since the previous call (each vehicle at most once).</summary>
    public IReadOnlyList<VehiclePosition> DrainChanged()
    {
        var result = new List<VehiclePosition>();
        foreach (var key in changed.Keys)
        {
            if (changed.TryRemove(key, out var position))
                result.Add(position);
        }
        return result;
    }

    /// <summary>Forgets vehicles that stopped reporting; returns their ids.</summary>
    public IReadOnlyList<string> Prune(DateTimeOffset now, TimeSpan maxAge)
    {
        var cutoff = (now - maxAge).ToUnixTimeSeconds();
        var removed = new List<string>();
        foreach (var (id, position) in latest)
        {
            if (position.Timestamp < cutoff && latest.TryRemove(new KeyValuePair<string, VehiclePosition>(id, position)))
            {
                changed.TryRemove(id, out _);
                removed.Add(id);
            }
        }
        return removed;
    }
}
