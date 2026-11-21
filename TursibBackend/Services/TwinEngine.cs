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
    /// <summary>Ce fracțiune din pasagerii din autobuz coboară la fiecare stație (1/8 = o călătorie medie de 8 stații).</summary>
    public double AlightingFraction { get; set; } = 1.0 / 8;

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
    /// <summary>Pasageri aflați acum în autobuz.</summary>
    public double Load { get; set; }
}

/// <summary>O perturbare programată: autobuzul <see cref="BusIndex"/> stă pe loc <see cref="HoldSeconds"/> începând cu <see cref="AtSecond"/>.</summary>
public record TwinDisturbance(int AtSecond, int BusIndex, int HoldSeconds);

/// <summary>What-if: <paramref name="Count"/> autobuze noi intră în circulație la <paramref name="AtSecond"/>, în cele mai mari goluri.</summary>
public record TwinBusInjection(int AtSecond, int Count);

/// <summary>
/// What-if: o stradă închisă pe porțiunea [<paramref name="FromMeters"/>, <paramref name="ToMeters"/>] a traseului (măsurată pe
/// sensul dus; se aplică ambelor sensuri), cu un ocol de <paramref name="DetourMeters"/> în plus. Stațiile de pe porțiune nu
/// sunt deservite; pasagerii lor merg la cea mai apropiată stație deschisă. Activă între secundele date (fără sfârșit = toată simularea).
/// </summary>
public record TwinClosure(double FromMeters, double ToMeters, double DetourMeters, int FromSecond = 0, int? ToSecond = null);

/// <summary>Scenariile what-if aplicate unei simulări.</summary>
public record TwinWhatIf(IReadOnlyList<TwinBusInjection>? Injections = null, IReadOnlyList<TwinClosure>? Closures = null);

/// <summary>Ce vede o strategie de control când un autobuz ajunge la o stație.</summary>
public record TwinStopContext(
    TwinBus Bus, int BusIndex, double GapAheadMeters, double GapBehindMeters, double IdealGapMeters,
    double Time, TwinConfig Config,
    /// <summary>headway-ul cu care a sosit autobuzul în stație: timpul de la plecarea autobuzului precedent (s)</summary>
    double HeadwaySeconds = 0,
    /// <summary>headway-ul mediu observat până acum în simulare (s); 0 la început</summary>
    double MeanHeadwaySeconds = 0);

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
    private readonly List<double> _initialPositions;
    private readonly List<TwinBus> _buses;
    private readonly List<TwinBusInjection> _injections;
    private readonly IReadOnlyList<TwinClosure> _closures;
    private readonly double[] _stopRate;
    private readonly bool[] _stopClosed;
    private string _closureState = "";
    private int _nextDisturbance;
    private int _nextInjection;

    public IReadOnlyList<TwinBus> Buses => _buses;
    /// <summary>Câte stații sunt închise acum (what-if).</summary>
    public int ClosedStops => _stopClosed.Count(c => c);
    /// <summary>Câte opriri au fost sărite din cauza străzilor închise.</summary>
    public long SkippedStopVisits { get; private set; }
    public double Time { get; private set; }
    public int StopCount { get; }
    public double StopSpacing { get; }
    public double IdealGapMeters => _config.LoopLengthMeters / Buses.Count;
    /// <summary>Suma reținerilor adăugate de strategia de control (fără perturbări): costul controlului.</summary>
    public double TotalControlHoldSeconds { get; private set; }
    /// <summary>Distanța totală parcursă de toată flota de la începutul simulării.</summary>
    public double DistanceTravelledMeters => Buses.Select((b, i) => b.Position - _initialPositions[i]).Sum();

    // --- Pasageri ---
    // Pasagerii sosesc uniform (rata λ) la fiecare stație. Un autobuz care vine după un interval H de la plecarea
    // precedentă găsește λH pasageri, care au așteptat în total λH²/2 secunde. Deci așteptarea medie pe pasager este
    // E[H²] / (2 E[H]) = E[H]/2 + Var(H) / (2 E[H]): neregularitatea headway-ului se plătește direct în așteptare.

    /// <summary>Pasageri preluați din stații.</summary>
    public double PassengersServed { get; private set; }
    /// <summary>Suma timpilor de așteptare în stație ai pasagerilor preluați (pasager·secundă).</summary>
    public double PassengerWaitSeconds { get; private set; }
    /// <summary>Întârzierea pasagerilor deja în autobuz cauzată de holding (pasager·secundă): costul controlului.</summary>
    public double OnBoardHoldPassengerSeconds { get; private set; }
    private double _headwaySum, _headwaySumSq;
    private long _headwayCount;

    /// <summary>Așteptarea medie în stație, pe pasager (secunde).</summary>
    public double MeanWaitSeconds => PassengersServed > 0 ? PassengerWaitSeconds / PassengersServed : 0;
    /// <summary>Headway-ul mediu în stații (timpul dintre plecarea unui autobuz și sosirea următorului), secunde.</summary>
    public double MeanHeadwaySeconds => _headwayCount > 0 ? _headwaySum / _headwayCount : 0;
    public double HeadwayVariance => _headwayCount > 1 ? Math.Max(0, _headwaySumSq / _headwayCount - MeanHeadwaySeconds * MeanHeadwaySeconds) : 0;
    /// <summary>Coeficientul de variație al headway-ului în timp (0 = autobuze perfect regulate).</summary>
    public double HeadwayCv => MeanHeadwaySeconds > 0 ? Math.Sqrt(HeadwayVariance) / MeanHeadwaySeconds : 0;
    /// <summary>Așteptarea în plus față de autobuze perfect regulate cu același headway mediu: Var(H) / (2 E[H]).</summary>
    public double ExcessWaitSeconds => MeanHeadwaySeconds > 0 ? HeadwayVariance / (2 * MeanHeadwaySeconds) : 0;
    /// <summary>Întârzierea totală pe pasager: așteptarea în stație plus timpul pierdut în autobuz din cauza holding-ului.</summary>
    public double TotalDelayPerPassengerSeconds =>
        PassengersServed > 0 ? (PassengerWaitSeconds + OnBoardHoldPassengerSeconds) / PassengersServed : 0;

    /// <param name="initialLoopPositions">poziția inițială a fiecărui autobuz pe buclă (0..2L), în ordinea indicilor</param>
    public TwinEngine(TwinConfig config, IReadOnlyList<double> initialLoopPositions, int seed,
        IEnumerable<TwinDisturbance>? disturbances = null, ITwinControl? control = null, TwinWhatIf? whatIf = null)
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

        _buses = initialLoopPositions
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

        _initialPositions = _buses.Select(b => b.Position).ToList();
        _injections = (whatIf?.Injections ?? Array.Empty<TwinBusInjection>()).OrderBy(i => i.AtSecond).ToList();
        _closures = whatIf?.Closures ?? Array.Empty<TwinClosure>();
        _stopRate = Enumerable.Repeat(config.PassengerArrivalRate, StopCount).ToArray();
        _stopClosed = new bool[StopCount];
        UpdateClosures();

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
        while (_nextInjection < _injections.Count && _injections[_nextInjection].AtSecond <= Time)
        {
            for (var k = 0; k < _injections[_nextInjection].Count; k++) InjectBus();
            _nextInjection++;
        }
        if (_closures.Count > 0) UpdateClosures();

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

            var speed = _config.CruiseSpeedMs * (1 + (_random.NextDouble() * 2 - 1) * _config.SpeedJitter)
                * DetourSpeedFactor(LoopPosition(bus));
            var maxByLeader = Buses.Count > 1 ? Math.Max(0, gapAhead[i] - _config.MinGapMeters) : double.MaxValue;
            var distance = Math.Min(speed * dt, maxByLeader);
            bus.Speed = distance / dt;

            if (bus.Position + distance >= bus.NextStopPosition)
            {
                bus.Position = bus.NextStopPosition;
                if (_stopClosed[StopIndex(bus.NextStopPosition)]) SkippedStopVisits++;   // stradă închisă: trece fără oprire
                else ArriveAtStop(bus, i, gapAhead[i], gapBehind[i]);
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
        var headway = Math.Max(0, Time - _lastDeparture[stop]);
        var rate = _stopRate[stop];
        var waiting = rate * headway;

        _headwaySum += headway;
        _headwaySumSq += headway * headway;
        _headwayCount++;
        PassengersServed += waiting;
        PassengerWaitSeconds += rate * headway * headway / 2;
        bus.Load = bus.Load * (1 - _config.AlightingFraction) + waiting;
        var dwell = _config.BaseDwellSeconds + _config.BoardingSecondsPerPassenger * waiting;
        dwell *= 1 + (_random.NextDouble() * 2 - 1) * _config.DwellJitter;
        dwell = Math.Clamp(dwell, 0, _config.MaxDwellSeconds);

        var extra = _control.ExtraHoldSeconds(new TwinStopContext(
            bus, index, gapAhead, gapBehind, IdealGapMeters, Time, _config, headway, MeanHeadwaySeconds));
        extra = Math.Max(0, extra);
        TotalControlHoldSeconds += extra;
        OnBoardHoldPassengerSeconds += bus.Load * extra;
        bus.DwellRemaining = dwell + extra;
    }

    /// <summary>What-if: un autobuz nou intră la jumătatea celui mai mare gol dintre autobuze.</summary>
    private void InjectBus()
    {
        var loop = _config.LoopLengthMeters;
        double start;
        if (_buses.Count == 0) start = 0;
        else
        {
            var ordered = _buses.Select(LoopPosition).OrderBy(p => p).ToList();
            var bestGap = -1.0;
            start = 0;
            for (var k = 0; k < ordered.Count; k++)
            {
                var gap = CircularForward(ordered[k], ordered[(k + 1) % ordered.Count], loop);
                if (ordered.Count == 1) gap = loop;
                if (gap > bestGap) { bestGap = gap; start = (ordered[k] + gap / 2) % loop; }
            }
        }
        _buses.Add(new TwinBus
        {
            Id = $"twin_bus_{_buses.Count}",
            Position = start,
            NextStopPosition = (Math.Floor(start / StopSpacing) + 1) * StopSpacing,
            Speed = _config.CruiseSpeedMs
        });
        _initialPositions.Add(start);
    }

    /// <summary>Porțiunile închise acum, pe bucla dus-întors (fiecare închidere apare pe ambele sensuri).</summary>
    private IEnumerable<(double From, double To, double Factor)> ActiveClosedIntervals()
    {
        var length = _config.RouteLengthMeters;
        foreach (var c in _closures)
        {
            if (Time < c.FromSecond || (c.ToSecond is { } end && Time >= end)) continue;
            var from = Math.Clamp(c.FromMeters, 0, length);
            var to = Math.Clamp(c.ToMeters, 0, length);
            if (to <= from) continue;
            // ocolul se parcurge în timpul în care s-ar fi parcurs porțiunea închisă: viteza efectivă scade proporțional
            var factor = (to - from) / (to - from + Math.Max(0, c.DetourMeters));
            yield return (from, to, factor);
            yield return (2 * length - to, 2 * length - from, factor);
        }
    }

    private double DetourSpeedFactor(double loopPosition)
    {
        if (_closures.Count == 0) return 1;
        foreach (var (from, to, factor) in ActiveClosedIntervals())
            if (loopPosition >= from && loopPosition < to) return factor;
        return 1;
    }

    /// <summary>Recalculează stațiile închise și mută cererea lor la cele mai apropiate stații deschise (jumătate în fiecare parte).</summary>
    private void UpdateClosures()
    {
        var intervals = ActiveClosedIntervals().ToList();
        var state = string.Join(";", intervals.Select(i => $"{i.From:F0}-{i.To:F0}"));
        if (state == _closureState) return;
        _closureState = state;

        for (var k = 0; k < StopCount; k++)
        {
            var pos = k * StopSpacing;
            _stopClosed[k] = intervals.Any(i => pos >= i.From && pos <= i.To);
            _stopRate[k] = _stopClosed[k] ? 0 : _config.PassengerArrivalRate;
        }
        if (_stopClosed.All(c => c)) return;   // totul închis: nimeni nu mai e deservit
        for (var k = 0; k < StopCount; k++)
        {
            if (!_stopClosed[k]) continue;
            int before = k, after = k;
            do before = (before - 1 + StopCount) % StopCount; while (_stopClosed[before]);
            do after = (after + 1) % StopCount; while (_stopClosed[after]);
            _stopRate[before] += _config.PassengerArrivalRate / 2;
            _stopRate[after] += _config.PassengerArrivalRate / 2;
        }
    }

    private int StopIndex(double stopPosition) =>
        ((int)Math.Round(stopPosition / StopSpacing) % StopCount + StopCount) % StopCount;

    private static double CircularForward(double from, double to, double loop)
    {
        var d = (to - from) % loop;
        return d < 0 ? d + loop : d;
    }
}
