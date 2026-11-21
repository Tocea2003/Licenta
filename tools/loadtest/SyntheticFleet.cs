namespace TursibLoadTest;

/// <summary>Un mesaj de poziție trimis de testul de încărcare.</summary>
public record LoadPosition(string Id, int RouteId, double Latitude, double Longitude, long Timestamp, double Speed,
    double Heading, int DirectionId, int NextStationId);

/// <summary>
/// O flotă sintetică: <c>buses</c> autobuze pe 20 de linii, care se mișcă în zona Sibiului. Fiecare mesaj are un număr
/// de secvență unic per autobuz, pus în <c>nextStationId</c> (câmp care trece neschimbat prin ingestie, Kafka și SignalR),
/// ca la recepție să se poată potrivi exact cu momentul trimiterii.
/// </summary>
public sealed class SyntheticFleet
{
    // zona Sibiu–Cisnădie: toate pozițiile trec validarea (coordonate reale, nu 0/0)
    public const double MinLat = 45.75, MaxLat = 45.82, MinLon = 24.09, MaxLon = 24.20;

    private readonly int _buses;
    private readonly int[] _sequence;
    private readonly double[] _phase;

    public SyntheticFleet(int buses, int seed = 1)
    {
        _buses = buses;
        _sequence = new int[buses];
        var random = new Random(seed);
        _phase = Enumerable.Range(0, buses).Select(_ => random.NextDouble() * Math.PI * 2).ToArray();
    }

    public int Count => _buses;

    public static string VehicleId(int index) => $"route_{1000 + index % 20}_bus_{index}";

    /// <summary>Următoarea poziție a autobuzului <paramref name="index"/> (secvența crește la fiecare apel).</summary>
    public LoadPosition Next(int index, DateTimeOffset now)
    {
        var seq = ++_sequence[index];
        var angle = _phase[index] + seq * 0.002;
        var lat = (MinLat + MaxLat) / 2 + (MaxLat - MinLat) / 2.2 * Math.Sin(angle);
        var lon = (MinLon + MaxLon) / 2 + (MaxLon - MinLon) / 2.2 * Math.Cos(angle);
        return new LoadPosition(VehicleId(index), 1000 + index % 20, lat, lon, now.ToUnixTimeSeconds(), 25,
            (angle * 180 / Math.PI) % 360, seq % 2, seq);
    }
}
