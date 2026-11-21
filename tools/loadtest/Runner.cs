using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace TursibLoadTest;

public sealed class LoadOptions
{
    public string Target { get; set; } = "http";              // http | mqtt
    public string Api { get; set; } = "http://localhost:5022/api";
    public string? IngestKey { get; set; }
    public string MqttHost { get; set; } = "localhost";
    public int MqttPort { get; set; } = 1883;
    public int Qos { get; set; }
    public int[] Buses { get; set; } = { 170 };
    public int IntervalMs { get; set; } = 500;
    public int DurationSeconds { get; set; } = 30;
    public int DrainSeconds { get; set; } = 5;
}

public record SecondSample(int Second, int Sent, int Received, double P95Ms, int Errors = 0);

public record StepResult(
    string Target, int Buses, int IntervalMs, int DurationSeconds, long Sent, long Received, double LossPercent,
    double SentPerSecond, double ReceivedPerSecond, double P50Ms, double P95Ms, double P99Ms, double MaxMs,
    long ServerAccepted, int PublishErrors, IReadOnlyList<SecondSample> Timeline);

/// <summary>
/// Un pas al testului: flota trimite timp de <c>DurationSeconds</c>, apoi se așteaptă <c>DrainSeconds</c> pentru
/// mesajele în tranzit. Latența = de la trimitere până la sosirea poziției pe hub-ul SignalR (cap-coadă, ca pe hartă).
/// </summary>
public sealed class Runner
{
    private readonly LoadOptions _o;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public Runner(LoadOptions options) => _o = options;

    public async Task<StepResult> RunStepAsync(int buses, CancellationToken ct)
    {
        var fleet = new SyntheticFleet(buses);
        var sentAt = new ConcurrentDictionary<(string, int), long>();
        var stats = new LatencyStats();
        var clock = Stopwatch.StartNew();
        var perSecondSent = new ConcurrentDictionary<int, int>();
        var perSecondReceived = new ConcurrentDictionary<int, int>();
        var perSecondLatency = new ConcurrentDictionary<int, LatencyStats>();
        long received = 0;

        var hub = new HubConnectionBuilder()
            .WithUrl(_o.Api.TrimEnd('/').Replace("/api", "") + "/hubs/buses", o => o.Transports = HttpTransportType.WebSockets)
            // backend-ul poate fi repornit în timpul testului (experimentele de toleranță la erori)
            .WithAutomaticReconnect(Enumerable.Range(0, 120).Select(_ => TimeSpan.FromSeconds(1)).ToArray())
            .Build();
        hub.On<List<JsonElement>>("positions", batch =>
        {
            var now = clock.Elapsed.TotalMilliseconds;
            foreach (var p in batch)
            {
                if (!p.TryGetProperty("id", out var id) || !p.TryGetProperty("nextStationId", out var seq) || seq.ValueKind != JsonValueKind.Number) continue;
                if (!sentAt.TryRemove((id.GetString()!, seq.GetInt32()), out var sentMs)) continue;
                var latency = now - sentMs;
                stats.Add(latency);
                var second = (int)(now / 1000);
                perSecondReceived.AddOrUpdate(second, 1, (_, n) => n + 1);
                perSecondLatency.GetOrAdd(second, _ => new LatencyStats()).Add(latency);
                Interlocked.Increment(ref received);
            }
        });
        await hub.StartAsync(ct);

        var acceptedBefore = await ServerAcceptedAsync(ct);
        IMqttClient? mqtt = null;
        var mqttOptions = new MqttClientOptionsBuilder().WithTcpServer(_o.MqttHost, _o.MqttPort)
            .WithClientId($"tursib-load-{Guid.NewGuid():N}"[..30]).Build();
        if (_o.Target == "mqtt")
        {
            mqtt = new MqttFactory().CreateMqttClient();
            await mqtt.ConnectAsync(mqttOptions, ct);
        }
        var perSecondErrors = new ConcurrentDictionary<int, int>();

        long sent = 0;
        var errors = 0;
        var ticks = _o.DurationSeconds * 1000 / _o.IntervalMs;
        for (var tick = 0; tick < ticks && !ct.IsCancellationRequested; tick++)
        {
            var tickStart = clock.Elapsed.TotalMilliseconds;
            var now = DateTimeOffset.UtcNow;
            var positions = Enumerable.Range(0, buses).Select(i => fleet.Next(i, now)).ToList();
            var second = (int)(tickStart / 1000);
            foreach (var p in positions) sentAt[(p.Id, p.NextStationId)] = (long)clock.Elapsed.TotalMilliseconds;

            try
            {
                if (mqtt != null)
                {
                    // ca un dispozitiv real: dacă brokerul a căzut, încearcă să se reconecteze; mesajele de acum se pierd
                    if (!mqtt.IsConnected) await mqtt.ConnectAsync(mqttOptions, ct);
                    foreach (var p in positions)
                    {
                        await mqtt.PublishAsync(new MqttApplicationMessageBuilder()
                            .WithTopic($"tursib/vehicles/{p.Id}/position")
                            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(p, Json))
                            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_o.Qos)
                            .Build(), ct);
                    }
                }
                else
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{_o.Api}/live/positions") { Content = JsonContent.Create(positions, options: Json) };
                    if (_o.IngestKey != null) request.Headers.Add("X-Ingest-Key", _o.IngestKey);
                    using var response = await _http.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode) errors++;
                }
                sent += positions.Count;
                perSecondSent.AddOrUpdate(second, positions.Count, (_, n) => n + positions.Count);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                errors++;
                perSecondErrors.AddOrUpdate(second, 1, (_, n) => n + 1);
                foreach (var p in positions) sentAt.TryRemove((p.Id, p.NextStationId), out _);
            }

            var wait = tickStart + _o.IntervalMs - clock.Elapsed.TotalMilliseconds;
            if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), ct);
        }

        await Task.Delay(TimeSpan.FromSeconds(_o.DrainSeconds), ct);
        var acceptedAfter = await ServerAcceptedAsync(ct);
        if (mqtt != null) await mqtt.DisconnectAsync();
        await hub.DisposeAsync();

        var seconds = perSecondSent.Keys.Concat(perSecondReceived.Keys).DefaultIfEmpty(0).Max();
        var timeline = Enumerable.Range(0, seconds + 1).Select(s => new SecondSample(s,
            perSecondSent.GetValueOrDefault(s), perSecondReceived.GetValueOrDefault(s),
            perSecondLatency.TryGetValue(s, out var l) ? Math.Round(l.Percentile(95), 1) : double.NaN,
            perSecondErrors.GetValueOrDefault(s))).ToList();

        var duration = (double)_o.DurationSeconds;
        return new StepResult(_o.Target, buses, _o.IntervalMs, _o.DurationSeconds, sent, received,
            sent == 0 ? 0 : Math.Round(100.0 * (sent - received) / sent, 2),
            Math.Round(sent / duration, 1), Math.Round(received / duration, 1),
            Math.Round(stats.Percentile(50), 1), Math.Round(stats.Percentile(95), 1), Math.Round(stats.Percentile(99), 1),
            Math.Round(stats.Max, 1), acceptedAfter - acceptedBefore, errors, timeline);
    }

    private async Task<long> ServerAcceptedAsync(CancellationToken ct)
    {
        try
        {
            var status = await _http.GetFromJsonAsync<JsonElement>($"{_o.Api}/live/status", ct);
            return status.GetProperty("live").GetProperty("acceptedTotal").GetInt64();
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return -1; }
    }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
