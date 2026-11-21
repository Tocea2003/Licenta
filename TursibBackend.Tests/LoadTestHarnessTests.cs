using System.Text;
using System.Text.Json;
using TursibBackend.Services;
using TursibLoadTest;
using TursibMessaging;
using Xunit;

namespace TursibBackend.Tests;

public class LoadTestHarnessTests
{
    [Fact]
    public void Percentiles_UseTheNearestRank()
    {
        var stats = new LatencyStats();
        foreach (var v in Enumerable.Range(1, 100)) stats.Add(v);
        Assert.Equal(50, stats.Percentile(50));
        Assert.Equal(95, stats.Percentile(95));
        Assert.Equal(100, stats.Percentile(100));
        Assert.Equal(100, stats.Max);
        Assert.True(double.IsNaN(new LatencyStats().Percentile(50)));
    }

    [Fact]
    public void SyntheticPositions_PassTheRealValidation_AndCarryAUniqueSequence()
    {
        var fleet = new SyntheticFleet(50);
        var now = DateTimeOffset.UtcNow;
        var state = new LiveBusState();
        var seen = new HashSet<(string, int)>();

        for (var tick = 0; tick < 20; tick++)
            for (var i = 0; i < fleet.Count; i++)
            {
                var p = fleet.Next(i, now);
                Assert.True(seen.Add((p.Id, p.NextStationId)), "secvența trebuie să fie unică per autobuz");

                // aceeași validare ca la ingestie (MQTT) și ca în backend (HTTP/Kafka)
                var json = JsonSerializer.SerializeToUtf8Bytes(p, Runner.Json);
                Assert.True(PositionParser.TryParse($"tursib/vehicles/{p.Id}/position", json, now, out _, out var reason), reason.ToString());
                Assert.Single(state.Apply(new[] { new BusPosition
                {
                    Id = p.Id, RouteId = p.RouteId, Latitude = p.Latitude, Longitude = p.Longitude, Timestamp = p.Timestamp,
                    NextStationId = p.NextStationId, DirectionId = p.DirectionId
                } }));
            }
    }

    [Fact]
    public void TheReportListsEveryStep()
    {
        var steps = new[]
        {
            new StepResult("http", 170, 500, 30, 10200, 10200, 0, 340, 340, 12, 30, 45, 80, 10200, 0, Array.Empty<SecondSample>()),
            new StepResult("http", 1000, 500, 30, 60000, 59000, 1.67, 2000, 1966.7, 40, 120, 300, 900, 59000, 2, Array.Empty<SecondSample>())
        };
        var md = Report.Markdown(new LoadOptions(), steps);
        Assert.Contains("| 170 | 340 |", md);
        Assert.Contains("| 1000 | 2000 | 1966.7/s | 1.67% |", md);
    }
}
