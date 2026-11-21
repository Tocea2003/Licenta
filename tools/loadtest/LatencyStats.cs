namespace TursibLoadTest;

/// <summary>Distribuția latențelor (ms): percentile prin sortare, suficient pentru zeci de mii de valori.</summary>
public sealed class LatencyStats
{
    private readonly List<double> _values = new();
    private readonly object _lock = new();

    public void Add(double milliseconds) { lock (_lock) _values.Add(milliseconds); }

    public int Count { get { lock (_lock) return _values.Count; } }

    /// <summary>Percentila p (0–100), interpolare la cel mai apropiat rang; NaN dacă nu sunt valori.</summary>
    public double Percentile(double p)
    {
        lock (_lock)
        {
            if (_values.Count == 0) return double.NaN;
            var sorted = _values.OrderBy(v => v).ToList();
            var rank = (int)Math.Ceiling(p / 100 * sorted.Count) - 1;
            return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
        }
    }

    public double Max { get { lock (_lock) return _values.Count == 0 ? double.NaN : _values.Max(); } }
    public double Mean { get { lock (_lock) return _values.Count == 0 ? double.NaN : _values.Average(); } }
}
