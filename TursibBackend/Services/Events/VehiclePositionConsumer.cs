using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Tursib.Contracts;

namespace TursibBackend.Services.Events;

/// <summary>
/// Reads validated positions from Kafka ("vehicle-positions") and fans them out
/// to the live state (SignalR, GTFS-RT) and the history queue.
/// Starts from the latest offset: the live view does not need old positions.
/// </summary>
public sealed class VehiclePositionConsumer : BackgroundService
{
    private readonly EventPipelineOptions options;
    private readonly LiveVehicleState state;
    private readonly HistorySampler sampler;
    private readonly HistoryQueue history;
    private readonly ILogger<VehiclePositionConsumer> logger;

    public VehiclePositionConsumer(
        IOptions<EventPipelineOptions> options,
        LiveVehicleState state,
        HistorySampler sampler,
        HistoryQueue history,
        ILogger<VehiclePositionConsumer> logger)
    {
        this.options = options.Value;
        this.state = state;
        this.sampler = sampler;
        this.history = history;
        this.logger = logger;
    }

    // Consume() blocks, so run the loop on its own thread instead of the startup path
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => Run(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void Run(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
            // Commit only what was actually processed
            EnableAutoOffsetStore = false,
            ClientId = "tursib-backend"
        };

        using var consumer = new ConsumerBuilder<string, byte[]>(config)
            .SetErrorHandler((_, e) => logger.LogWarning("Kafka: {Reason}", e.Reason))
            .SetPartitionsAssignedHandler((_, partitions) =>
                logger.LogInformation("Kafka assigned {Count} partitions of {Topic}", partitions.Count, Topics.KafkaVehiclePositions))
            .Build();

        consumer.Subscribe(Topics.KafkaVehiclePositions);
        logger.LogInformation("Consuming {Topic} from {Servers}", Topics.KafkaVehiclePositions, options.BootstrapServers);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message?.Value is { } value)
                    {
                        var position = ContractJson.TryDeserialize(value);
                        if (position is not null && state.Upsert(position) && sampler.ShouldRecord(position))
                            history.TryEnqueue(position);
                    }
                    if (result is not null)
                        consumer.StoreOffset(result);
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning("Kafka consume error: {Reason}", ex.Error.Reason);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        finally
        {
            consumer.Close();
        }
    }
}
