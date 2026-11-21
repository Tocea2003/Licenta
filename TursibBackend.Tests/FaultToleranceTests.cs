using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Client;
using TursibBackend.Data;
using TursibBackend.Services;
using TursibMessaging;
using Xunit;

namespace TursibBackend.Tests;

public class FaultToleranceTests
{
    /// <summary>Simulează o bază de date indisponibilă: primele <c>failures</c> salvări aruncă.</summary>
    private sealed class FlakySaves(int failures) : SaveChangesInterceptor
    {
        private int _left = failures;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default) =>
            Interlocked.Decrement(ref _left) >= 0
                ? throw new InvalidOperationException("baza de date nu răspunde")
                : base.SavingChangesAsync(e, r, ct);
    }

    [Fact]
    public async Task HistoryWriter_KeepsThePositions_WhileTheDatabaseIsDown_AndWritesThemWhenItReturns()
    {
        var services = new ServiceCollection();
        var db = Guid.NewGuid().ToString();
        var flaky = new FlakySaves(failures: 2);
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(db).AddInterceptors(flaky));
        using var sp = services.BuildServiceProvider();

        var options = new HistoryOptions { Enabled = true, SampleSeconds = 0, RetentionDays = 30 };
        var buffer = new PositionHistoryBuffer(options);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        buffer.Offer(Enumerable.Range(0, 5).Select(i => new BusPosition { Id = $"route_1_bus_{i}", RouteId = 1, Latitude = 45.7, Longitude = 24.1, Timestamp = now }));
        var writer = new PositionHistoryWriter(buffer, options, sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<PositionHistoryWriter>.Instance);

        await writer.FlushAsync(default);                  // eșec 1
        await writer.FlushAsync(default);                  // eșec 2
        Assert.Equal(5, buffer.Count);                     // nimic pierdut
        Assert.NotNull(writer.LastError);

        await writer.FlushAsync(default);                  // baza a revenit
        Assert.Equal(0, buffer.Count);
        Assert.Null(writer.LastError);
        using var scope = sp.CreateScope();
        Assert.Equal(5, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().VehiclePositionHistory.CountAsync());
    }

    private sealed class Throwing : IBusPositionHandler
    {
        public Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct) => throw new InvalidOperationException("handler căzut");
    }

    private sealed class Counting : IBusPositionHandler
    {
        public int Batches;
        public Task HandleAsync(IReadOnlyList<BusPosition> positions, CancellationToken ct) { Interlocked.Increment(ref Batches); return Task.CompletedTask; }
    }

    [Fact]
    public async Task AFailingConsumer_DoesNotStopTheOthers()
    {
        var bus = new LiveEventBus();
        var counting = new Counting();
        var dispatcher = new LiveEventDispatcher(bus, new IBusPositionHandler[] { new Throwing(), counting }, NullLogger<LiveEventDispatcher>.Instance);
        await dispatcher.StartAsync(default);
        try
        {
            for (var i = 0; i < 3; i++)
                bus.Publish(new[] { new BusPosition { Id = "route_1_bus_0", RouteId = 1, Latitude = 45.7, Longitude = 24.1, Timestamp = i + 1 } });
            for (var i = 0; i < 50 && counting.Batches < 3; i++) await Task.Delay(20);
            Assert.Equal(3, counting.Batches);
        }
        finally { await dispatcher.StopAsync(default); }
    }

    private static string? MosquittoBinary() =>
        Environment.GetEnvironmentVariable("TEST_MOSQUITTO_BIN")
        ?? new[] { "/usr/sbin/mosquitto", "/usr/bin/mosquitto", "/opt/homebrew/sbin/mosquitto" }.FirstOrDefault(File.Exists);

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static Process StartBroker(string binary, int port, string dir)
    {
        var conf = Path.Combine(dir, "m.conf");
        File.WriteAllText(conf, $"listener {port} 127.0.0.1\nallow_anonymous true\npersistence false\nuser root\n");
        var p = Process.Start(new ProcessStartInfo(binary, $"-c {conf}") { RedirectStandardOutput = true, RedirectStandardError = true })!;
        Thread.Sleep(500);
        return p;
    }

    /// <summary>
    /// Brokerul MQTT cade și revine: subscriber-ul (folosit de worker și de ingestia integrată) se reconectează singur și
    /// reia livrarea. Rulează cu un Mosquitto real dacă e instalat (sau TEST_MOSQUITTO_BIN); altfel e sărit.
    /// </summary>
    [Fact]
    public async Task MqttSubscriber_ReconnectsAfterTheBrokerRestarts()
    {
        var binary = MosquittoBinary();
        if (binary == null) return;   // fără broker local: testul nu are ce verifica (CI îl acoperă prin jobul event-infra)

        var dir = Directory.CreateTempSubdirectory("mqtt-fault").FullName;
        var port = FreePort();
        var broker = StartBroker(binary, port, dir);
        var received = 0;
        var subscriber = new MqttSubscriber(new MqttOptions { Host = "127.0.0.1", Port = port },
            (_, _, _) => { Interlocked.Increment(ref received); return Task.CompletedTask; }, NullLogger.Instance);
        await subscriber.StartAsync(default);
        try
        {
            async Task<bool> DeliversAsync(TimeSpan within)
            {
                var before = received;
                var deadline = DateTime.UtcNow + within;
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        using var device = new MqttFactory().CreateMqttClient();
                        await device.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build());
                        await device.PublishStringAsync("tursib/vehicles/route_1_bus_0/position", "{}");
                        await device.DisconnectAsync();
                    }
                    catch (Exception) { /* brokerul încă pornește */ }
                    await Task.Delay(300);
                    if (received > before) return true;
                }
                return false;
            }

            Assert.True(await DeliversAsync(TimeSpan.FromSeconds(10)), "livrare înainte de cădere");

            broker.Kill();
            await broker.WaitForExitAsync();
            await Task.Delay(1500);
            var restartedAt = Stopwatch.StartNew();
            broker = StartBroker(binary, port, dir);

            // reconectarea se încearcă la 5 s: în cel mult ~10 s mesajele circulă din nou
            Assert.True(await DeliversAsync(TimeSpan.FromSeconds(15)), "livrare după revenirea brokerului");
            Assert.True(restartedAt.Elapsed < TimeSpan.FromSeconds(15));
        }
        finally
        {
            await subscriber.StopAsync(default);
            if (!broker.HasExited) broker.Kill();
        }
    }
}
