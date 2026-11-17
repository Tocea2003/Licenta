namespace TursibBackend.Services;

// Digital twin al unei linii: simulare deterministă (seed) a flotei pe bucla dus-întors, rulabilă mult mai
// repede decât timpul real. Mecanismul care produce bunching e cel clasic: un autobuz întârziat găsește mai
// mulți pasageri în stații, stă mai mult la urcare, întârzie și mai mult, iar cel din spate îl ajunge din urmă.

/// <summary>Parametrii fizici ai liniei și ai cererii de pasageri.</summary>
public class TwinConfig
{
    /// <summary>Lungimea traseului dus (m); bucla dus-întors are dublu.</summary>
    public double RouteLengthMeters { get; set; }
    /// <summary>Distanța dorită între stații pe buclă (m); se rotunjește ca stațiile să împartă exact bucla.</summary>
    public double StopSpacingMeters { get; set; } = 400;
    public double CruiseSpeedMs { get; set; } = 7;          // ≈ 25 km/h
    /// <summary>Variația aleatoare a vitezei pe pas (±fracțiune).</summary>
    public double SpeedJitter { get; set; } = 0.15;
    public double BaseDwellSeconds { get; set; } = 8;
    public double BoardingSecondsPerPassenger { get; set; } = 2;
    public double MaxDwellSeconds { get; set; } = 90;
    /// <summary>Variația aleatoare a timpului de staționare (±fracțiune): sursa de zgomot care declanșează bunching-ul.</summary>
    public double DwellJitter { get; set; } = 0.3;
    /// <summary>Pasageri care sosesc pe secundă la o stație.</summary>
    public double PassengerArrivalRate { get; set; } = 0.008;
    /// <summary>Distanța minimă pe care o păstrează un autobuz față de cel din față (nu se depășesc).</summary>
    public double MinGapMeters { get; set; } = 15;

    public double LoopLengthMeters => 2 * RouteLengthMeters;
}

public class TwinBus
{
    public string Id { get; init; } = string.Empty;
    /// <summary>Distanța totală parcursă (nemodulată); poziția pe buclă = Position mod lungimea buclei.</summary>
    public double Position { get; set; }
    public double NextStopPosition { get; set; }
    public double DwellRemaining { get; set; }
    /// <summary>Oprire forțată (avarie, trafic, holding) — nu se mișcă cât timp e &gt; 0.</summary>
    public double HoldRemaining { get; set; }
    public double Speed { get; set; }
}

/// <summary>O perturbare programată: autobuzul <see cref="BusIndex"/> stă pe loc <see cref="HoldSeconds"/> începând cu <see cref="AtSecond"/>.</summary>
public record TwinDisturbance(int AtSecond, int BusIndex, int HoldSeconds);

/// <summary>Ce vede o strategie de control când un autobuz ajunge la o stație.</summary>
public record TwinStopContext(
    TwinBus Bus, int BusIndex, double GapAheadMeters, double GapBehindMeters, double IdealGapMeters,
    double Time, TwinConfig Config);

/// <summary>Strategie de control: la sosirea într-o stație poate cere o oprire suplimentară (holding).</summary>
public interface ITwinControl
{
    double ExtraHoldSeconds(TwinStopContext context);
}

public sealed class NoControl : ITwinControl
{
    public double ExtraHoldSeconds(TwinStopContext context) => 0;
}

public class TwinEngine
{
    private readonly TwinConfig _config;
    private readonly ITwinControl _control;
    private readonly Random _random;
    private readonly List<TwinDisturbance> _disturbances;
    private readonly double[] _lastDeparture;
    private int _nextDisturbance;

    public IReadOnlyList<TwinBus> Buses { get; }
    public double Time { get; private set; }
    public int StopCount { get; }
    public double StopSpacing { get; }
    public double IdealGapMeters => _config.LoopLengthMeters / Buses.Count;

    /// <param name="initialLoopPositions">poziția inițială a fiecărui autobuz pe buclă (0..2L), în ordinea indicilor</param>
    public TwinEngine(TwinConfig config, IReadOnlyList<double> initialLoopPositions, int seed,
        IEnumerable<TwinDisturbance>? disturbances = null, ITwinControl? control = null)
    {
        if (config.RouteLengthMeters <= 0) throw new ArgumentException("Lungimea traseului trebuie să fie pozitivă.", nameof(config));
        if (initialLoopPositions.Count < 1) throw new ArgumentException("Cel puțin un autobuz.", nameof(initialLoopPositions));

        _config = config;
        _control = control ?? new NoControl();
        _random = new Random(seed);
        _disturbances = (disturbances ?? Array.Empty<TwinDisturbance>()).OrderBy(d => d.AtSecond).ToList();

        var loop = config.LoopLengthMeters;
        StopCount = Math.Max(2, (int)Math.Round(loop / config.StopSpacingMeters));
        StopSpacing = loop / StopCount;

        Buses = initialLoopPositions
            .Select((p, i) =>
            {
                var pos = ((p % loop) + loop) % loop;
                return new TwinBus
                {
                    Id = $"twin_bus_{i}",
                    Position = pos,
                    NextStopPosition = (Math.Floor(pos / StopSpacing) + 1) * StopSpacing,
                    Speed = config.CruiseSpeedMs
                };
            })
            .ToList();

        // La început, la fiecare stație aștepta exact cât ar fi trecut de la ultimul autobuz care a depășit-o.
        _lastDeparture = new double[StopCount];
        for (var k = 0; k < StopCount; k++)
        {
            var stopPos = k * StopSpacing;
            var sinceLast = Buses.Min(b => CircularForward(stopPos, LoopPosition(b), loop)) / config.CruiseSpeedMs;
            _lastDeparture[k] = -sinceLast;
        }
    }

    public double LoopPosition(TwinBus bus) => bus.Position % _config.LoopLengthMeters;

    /// <summary>Avansează simularea cu <paramref name="dt"/> secunde.</summary>
    public void Step(double dt = 1)
    {
        var loop = _config.LoopLengthMeters;

        while (_nextDisturbance < _disturbances.Count && _disturbances[_nextDisturbance].AtSecond <= Time)
        {
            var d = _disturbances[_nextDisturbance++];
            if (d.BusIndex >= 0 && d.BusIndex < Buses.Count) Buses[d.BusIndex].HoldRemaining += d.HoldSeconds;
        }

        // golul față de autobuzul din față, calculat înainte de mișcare (nu se depășesc)
        var gapAhead = new double[Buses.Count];
        var gapBehind = new double[Buses.Count];
        if (Buses.Count > 1)
        {
            var order = Enumerable.Range(0, Buses.Count).OrderBy(i => LoopPosition(Buses[i])).ToList();
            for (var k = 0; k < order.Count; k++)
            {
                var i = order[k];
                var ahead = order[(k + 1) % order.Count];
                var gap = CircularForward(LoopPosition(Buses[i]), LoopPosition(Buses[ahead]), loop);
                if (gap <= 0) gap = loop; // poziții identice: nu blocăm, dar considerăm bucla întreagă
                gapAhead[i] = gap;
                gapBehind[ahead] = gap;
            }
        }
        else
        {
            gapAhead[0] = gapBehind[0] = loop;
        }

        for (var i = 0; i < Buses.Count; i++)
        {
            var bus = Buses[i];
            if (bus.HoldRemaining > 0) { bus.HoldRemaining = Math.Max(0, bus.HoldRemaining - dt); bus.Speed = 0; continue; }
            if (bus.DwellRemaining > 0)
            {
                bus.DwellRemaining -= dt;
                bus.Speed = 0;
                if (bus.DwellRemaining <= 0) _lastDeparture[StopIndex(bus.NextStopPosition - StopSpacing)] = Time + dt;
                continue;
            }

            var speed = _config.CruiseSpeedMs * (1 + (_random.NextDouble() * 2 - 1) * _config.SpeedJitter);
            var maxByLeader = Buses.Count > 1 ? Math.Max(0, gapAhead[i] - _config.MinGapMeters) : double.MaxValue;
            var distance = Math.Min(speed * dt, maxByLeader);
            bus.Speed = distance / dt;

            if (bus.Position + distance >= bus.NextStopPosition)
            {
                bus.Position = bus.NextStopPosition;
                ArriveAtStop(bus, i, gapAhead[i], gapBehind[i]);
                bus.NextStopPosition += StopSpacing;
            }
            else
            {
                bus.Position += distance;
            }
        }

        Time += dt;
    }

    private void ArriveAtStop(TwinBus bus, int index, double gapAhead, double gapBehind)
    {
        var stop = StopIndex(bus.NextStopPosition);
        var waiting = _config.PassengerArrivalRate * Math.Max(0, Time - _lastDeparture[stop]);
        var dwell = _config.BaseDwellSeconds + _config.BoardingSecondsPerPassenger * waiting;
        dwell *= 1 + (_random.NextDouble() * 2 - 1) * _config.DwellJitter;
        dwell = Math.Clamp(dwell, 0, _config.MaxDwellSeconds);

        var extra = _control.ExtraHoldSeconds(new TwinStopContext(bus, index, gapAhead, gapBehind, IdealGapMeters, Time, _config));
        bus.DwellRemaining = dwell + Math.Max(0, extra);
    }

    private int StopIndex(double stopPosition) =>
        ((int)Math.Round(stopPosition / StopSpacing) % StopCount + StopCount) % StopCount;

    private static double CircularForward(double from, double to, double loop)
    {
        var d = (to - from) % loop;
        return d < 0 ? d + loop : d;
    }
}
