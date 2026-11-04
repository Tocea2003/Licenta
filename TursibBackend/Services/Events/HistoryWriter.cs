using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Services.Events;

/// <summary>
/// Writes sampled positions to PostgreSQL in batches (every 2 s) for replay,
/// and deletes rows older than the retention period once a day.
/// </summary>
public sealed class HistoryWriter : BackgroundService
{
    private const int MaxBatch = 5_000;
    private static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CleanupEvery = TimeSpan.FromHours(24);

    private readonly EventPipelineOptions options;
    private readonly HistoryQueue queue;
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<HistoryWriter> logger;

    public HistoryWriter(
        IOptions<EventPipelineOptions> options,
        HistoryQueue queue,
        IServiceScopeFactory scopes,
        ILogger<HistoryWriter> logger)
    {
        this.options = options.Value;
        this.queue = queue;
        this.scopes = scopes;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextCleanup = DateTimeOffset.UtcNow;
        using var timer = new PeriodicTimer(FlushEvery);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await FlushAsync(stoppingToken);

                if (DateTimeOffset.UtcNow >= nextCleanup)
                {
                    nextCleanup = DateTimeOffset.UtcNow + CleanupEvery;
                    await CleanupAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Writing position history failed");
            }
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var batch = new List<VehiclePositionRecord>();
        while (batch.Count < MaxBatch && queue.Reader.TryRead(out var p))
        {
            batch.Add(new VehiclePositionRecord
            {
                VehicleId = p.VehicleId,
                RouteId = p.RouteId,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Speed = p.Speed,
                Heading = p.Heading,
                Occupancy = p.Occupancy,
                RecordedAt = DateTimeOffset.FromUnixTimeSeconds(p.Timestamp).UtcDateTime
            });
        }
        if (batch.Count == 0) return;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.VehiclePositionHistory.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.HistoryRetentionDays);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.VehiclePositionHistory.Where(r => r.RecordedAt < cutoff).ExecuteDeleteAsync(ct);
        if (deleted > 0)
            logger.LogInformation("Deleted {Count} history rows older than {Days} days", deleted, options.HistoryRetentionDays);
    }
}
