using System.Threading.Channels;
using Tursib.Contracts;

namespace TursibBackend.Services.Events;

/// <summary>
/// Hand-off between the Kafka consumer and the database writer. Bounded so a slow
/// database drops the oldest samples instead of growing memory.
/// </summary>
public sealed class HistoryQueue
{
    private readonly Channel<VehiclePosition> channel = Channel.CreateBounded<VehiclePosition>(
        new BoundedChannelOptions(20_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<VehiclePosition> Reader => channel.Reader;

    public bool TryEnqueue(VehiclePosition position) => channel.Writer.TryWrite(position);
}
