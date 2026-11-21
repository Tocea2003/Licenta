using System.Text;
using System.Text.Json;
using TursibLoadTest;

// Test de încărcare pentru platforma live. Exemple:
//   dotnet run --project tools/loadtest -- --target http --ingest-key dev-ingest-key --buses 170,500,1000 --duration 30
//   dotnet run --project tools/loadtest -- --target mqtt --mqtt-host localhost --buses 170 --duration 120 --out ../../docs/results
var options = new LoadOptions();
string? outDir = null;
string? name = null;
for (var i = 0; i < args.Length - 1; i += 2)
{
    var value = args[i + 1];
    switch (args[i])
    {
        case "--target": options.Target = value; break;
        case "--api": options.Api = value; break;
        case "--ingest-key": options.IngestKey = value; break;
        case "--mqtt-host": options.MqttHost = value; break;
        case "--mqtt-port": options.MqttPort = int.Parse(value); break;
        case "--qos": options.Qos = int.Parse(value); break;
        case "--buses": options.Buses = value.Split(',').Select(int.Parse).ToArray(); break;
        case "--interval-ms": options.IntervalMs = int.Parse(value); break;
        case "--duration": options.DurationSeconds = int.Parse(value); break;
        case "--drain": options.DrainSeconds = int.Parse(value); break;
        case "--out": outDir = value; break;
        case "--name": name = value; break;
        default: Console.Error.WriteLine($"Opțiune necunoscută: {args[i]}"); return 2;
    }
}

var runner = new Runner(options);
var results = new List<StepResult>();
foreach (var buses in options.Buses)
{
    Console.WriteLine($"▶ {options.Target}: {buses} autobuze la {options.IntervalMs} ms, {options.DurationSeconds} s...");
    var r = await runner.RunStepAsync(buses, CancellationToken.None);
    results.Add(r);
    Console.WriteLine($"   trimise {r.Sent} ({r.SentPerSecond}/s), pe hartă {r.Received} ({r.LossPercent}% pierdute), " +
                      $"latență p50 {r.P50Ms} ms, p95 {r.P95Ms} ms, p99 {r.P99Ms} ms, max {r.MaxMs} ms, erori {r.PublishErrors}");
}

if (outDir != null)
{
    Directory.CreateDirectory(outDir);
    name ??= $"load-{options.Target}";
    File.WriteAllText(Path.Combine(outDir, name + ".json"),
        JsonSerializer.Serialize(results, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    File.WriteAllText(Path.Combine(outDir, name + ".md"), Report.Markdown(options, results));
    Console.WriteLine($"✅ Rezultate în {outDir}/{name}.md");
}
return 0;

public static class Report
{
    public static string Markdown(LoadOptions o, IReadOnlyList<StepResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"| autobuze | mesaje/s trimise | ajunse pe hartă | pierdute | latență p50 | p95 | p99 | max | erori |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
            sb.AppendLine($"| {r.Buses} | {r.SentPerSecond} | {r.ReceivedPerSecond}/s | {r.LossPercent}% | {r.P50Ms} ms | {r.P95Ms} ms | {r.P99Ms} ms | {r.MaxMs} ms | {r.PublishErrors} |");
        return sb.ToString();
    }
}
