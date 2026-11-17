using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TursibBackend.Data;
using TursibBackend.Models;

namespace TursibBackend.Services;

/// <summary>Secțiunea de configurare „History”. Dezactivat implicit: istoricul consumă spațiu în baza de date.</summary>
public class HistoryOptions
{
    public bool Enabled { get; set; }
    /// <summary>Se păstrează cel mult o poziție per autobuz la acest interval.</summary>
    public int SampleSeconds { get; set; } = 15;
    public int RetentionDays { get; set; } = 3;
    public int FlushSeconds { get; set; } = 5;
    /// <summary>Plafon al tamponului din memorie dacă baza de date nu răspunde (cele mai vechi se pierd).</summary>
    public int MaxBuffered { get; set; } = 50_000;
}

/// <summary>Tampon cu eșantionare: primește toate pozițiile, reține una per autobuz la fiecare <c>SampleSeconds</c>.</summary>
public class PositionHistoryBuffer
{
    private readonly HistoryOptions _options;
    private readonly ConcurrentDictionary<string, long> _lastRecorded = new();
    private readonly ConcurrentQueue<VehiclePositionRecord> _queue = new();

    public PositionHistoryBuffer(HistoryOptions options) => _options = options;

    public int Count => _queue.Count;

    public void Offer(IEnumerable<BusPosition> positions)
    {
        foreach (var p in positions)
        {
            if (_lastRecorded.TryGetValue(p.Id, out var last) && p.Timestamp - last < _options.SampleSeconds) continue;
            _lastRecorded[p.Id] = p.Timestamp;
            _queue.Enqueue(new VehiclePositionRecord
            {
                VehicleId = p.Id, RouteId = p.RouteId, Latitude = p.Latitude, Longitude = p.Longitude,
                Speed = p.Speed, Heading = p.Heading, Occupancy = p.Occupancy, Status = p.Status, DirectionId = p.DirectionId,
                RecordedAt = DateTimeOffset.FromUnixTimeSeconds(p.Timestamp).UtcDateTime
            });
        }
        while (_queue.Count > _options.MaxBuffered) _queue.TryDequeue(out _);
    }

    public List<VehiclePositionRecord> Drain(int max = 5000)
    {
        var items = new List<VehiclePositionRecord>();
        while (items.Count < max && _queue.TryDequeue(out var r)) items.Add(r);
        return items;
    }

    /// <summary>Repune în coadă (la început nu se poate cu ConcurrentQueue; ordinea nu contează, ne bazăm pe RecordedAt).</summary>
    public void Requeue(IEnumerable<VehiclePositionRecord> records)
    {
        foreach (var r in records) _queue.Enqueue(r);
    }
}

/// <summary>Handler pe magistrala de evenimente: pune pozițiile acceptate în tampon.</summary>
public class PositionHistoryHandler : IBusPositionHandler
{
    private readonly PositionHistoryBuffer _buffer;
    public PositionHistoryHandler(PositionHistoryBuffer buffer) => _buffer = buffer;

    public Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct)
    {
        _buffer.Offer(positions);
        return Task.CompletedTask;
    }
}

/// <summary>Scrie periodic tamponul în PostgreSQL (în loturi) și șterge pozițiile mai vechi decât retenția.</summary>
public class PositionHistoryWriter : BackgroundService
{
    private readonly PositionHistoryBuffer _buffer;
    private readonly HistoryOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PositionHistoryWriter> _logger;
    private DateTime _lastPurge = DateTime.MinValue;

    public PositionHistoryWriter(PositionHistoryBuffer buffer, HistoryOptions options, IServiceScopeFactory scopes,
        ILogger<PositionHistoryWriter> logger)
    {
        _buffer = buffer;
        _options = options;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.FlushSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }
        // la oprire scriem ce a rămas în tampon
        await FlushAsync(CancellationToken.None);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        var records = _buffer.Drain();
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (records.Count > 0)
            {
                db.VehiclePositionHistory.AddRange(records);
                await db.SaveChangesAsync(ct);
            }
            if (DateTime.UtcNow - _lastPurge > TimeSpan.FromHours(1))
            {
                _lastPurge = DateTime.UtcNow;
                await PurgeAsync(db, DateTime.UtcNow.AddDays(-_options.RetentionDays), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nu am putut salva {Count} poziții în istoric; le reîncerc", records.Count);
            _buffer.Requeue(records);
        }
    }

    public static async Task PurgeAsync(ApplicationDbContext db, DateTime olderThan, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await db.VehiclePositionHistory.Where(r => r.RecordedAt < olderThan).ExecuteDeleteAsync(ct);
        }
        else
        {
            // InMemory (teste) nu suportă ExecuteDelete
            db.VehiclePositionHistory.RemoveRange(db.VehiclePositionHistory.Where(r => r.RecordedAt < olderThan));
            await db.SaveChangesAsync(ct);
        }
    }
}
