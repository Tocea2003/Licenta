using TursibBackend.Data;
using TursibBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Data.Sqlite;
using RouteModel = TursibBackend.Models.Route;

namespace TursibBackend.Services
{
    /// <summary>
    /// Service pentru calcularea rutelor optime folosind algoritmul Dijkstra adaptat pentru transport public.
    /// Implementează un graf multi-modal cu suport pentru transferuri și walking între stații.
    /// </summary>
    public class RouteCalculatorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RouteCalculatorService> _logger;
        private const double WALKING_SPEED_KM_PER_HOUR = 5.0; // Viteza medie de mers pe jos
        private const double MAX_WALKING_DISTANCE_KM = 0.5; // Distanța maximă pentru walking (500m)
        private const int TRANSFER_PENALTY_MINUTES = 5; // Penalitate pentru fiecare transfer
        private const int AVG_BUS_SPEED_KM_PER_HOUR = 25; // Viteza medie autobuz în oraș
        private const int STATION_STOP_TIME_MINUTES = 1; // Timp de oprire la fiecare stație
        private const int MIN_TRANSFER_SECONDS = 60; // Timp minim de transfer între două vehicule la aceeași stație
        private const int MAX_SCHEDULE_SEARCH_SECONDS = 12 * 3600; // Cât în viitor căutăm o cursă (12h)
        private const int MAX_SCHEDULE_ALTERNATIVES = 5; // Câte plecări reale enumerăm (gen Google)

        private record GraphCacheEntry(TransportGraph Graph, List<RouteModel> AllRoutes, List<Station> AllStations);

        public RouteCalculatorService(ApplicationDbContext context, IMemoryCache cache, ILogger<RouteCalculatorService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        private async Task<GraphCacheEntry> GetOrBuildGraphAsync(HashSet<int>? activeRouteIds = null)
        {
            // Cache key include ziua săptămânii pentru grafuri diferite per zi
            var cacheKeySuffix = activeRouteIds != null
                ? string.Join(",", activeRouteIds.OrderBy(x => x))
                : "all";
            var cacheKey = $"transport_graph_{cacheKeySuffix}";

            if (_cache.TryGetValue(cacheKey, out GraphCacheEntry? cached) && cached != null)
                return cached;

            var allRoutes = await _context.Routes
                .AsNoTracking()
                .Include(r => r.RouteStations)
                .ThenInclude(rs => rs.Station)
                .ToListAsync();
            var allStations = await _context.Stations.AsNoTracking().ToListAsync();

            var graph = BuildTransportGraph(allRoutes, allStations, activeRouteIds);
            var entry = new GraphCacheEntry(graph, allRoutes, allStations);
            _cache.Set(cacheKey, entry, TimeSpan.FromMinutes(5));
            return entry;
        }

        #region Public API Methods

        /// <summary>
        /// Calculează ruta optimă între două stații folosind algoritmul Dijkstra.
        /// </summary>
        public async Task<CalculatedRoute?> CalculateOptimalRoute(
            int startStationId,
            int endStationId,
            DateTime? departureTime = null,
            DateTime? arrivalTime = null)
        {
            var routes = await CalculateAlternativeRoutes(startStationId, endStationId, departureTime, arrivalTime);
            return routes.FirstOrDefault();
        }

        /// <summary>
        /// Calculează multiple rute alternative folosind variații ale algoritmului Dijkstra
        /// cu diferite penalități pentru a obține diversitate în rezultate.
        /// </summary>
        public async Task<List<CalculatedRoute>> CalculateAlternativeRoutes(
            int startStationId,
            int endStationId,
            DateTime? departureTime = null,
            DateTime? arrivalTime = null)
        {
            var alternativeRoutes = new List<CalculatedRoute>();

            // Determină data efectivă pentru filtrarea pe ziua săptămânii
            var effectiveDate = arrivalTime ?? departureTime ?? DateTime.Now;

            // ── PLANIFICATOR PE ORAR REAL (time-dependent Dijkstra peste StopTimes) ──
            // Folosește orele reale de plecare/sosire ale curselor, ca Google Maps.
            // Dacă datele de orar lipsesc (ex. teste in-memory, GTFS neimportat),
            // se face fallback la Dijkstra estimativ de mai jos.
            try
            {
                var scheduleIndex = await GetOrBuildScheduleIndexAsync(effectiveDate);
                if (scheduleIndex != null && scheduleIndex.Trips.Count > 0)
                {
                    var scheduled = PlanScheduleBasedRoutes(
                        scheduleIndex, startStationId, endStationId, departureTime, arrivalTime, effectiveDate);
                    if (scheduled.Count > 0)
                    {
                        _logger.LogInformation("🕒 Planificator pe orar: {Count} opțiuni reale între {Start} și {End}",
                            scheduled.Count, startStationId, endStationId);
                        return scheduled;
                    }
                    _logger.LogInformation("🕒 Planificator pe orar nu a găsit curse — fallback la estimativ");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🕒 Planificatorul pe orar a eșuat — fallback la Dijkstra estimativ");
            }

            // Obține rutele active în ziua selectată (din GTFS calendar)
            HashSet<int>? activeRouteIds = null;
            try
            {
                activeRouteIds = await GetActiveRouteIdsForDate(effectiveDate);
                if (activeRouteIds.Count == 0)
                {
                    _logger.LogWarning("📅 Nu s-au găsit rute active pentru data {Date} ({Day}), fallback la toate rutele",
                        effectiveDate.ToString("yyyy-MM-dd"), effectiveDate.DayOfWeek);
                    activeRouteIds = null; // Fallback: include toate rutele
                }
                else
                {
                    _logger.LogInformation("📅 Rute active pentru {Day} ({Date}): {Count} rute",
                        effectiveDate.DayOfWeek, effectiveDate.ToString("yyyy-MM-dd"), activeRouteIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "📅 Eroare la interogarea calendarului, fallback la toate rutele");
                activeRouteIds = null;
            }

            // Load graph and route data (cached for 5 minutes, per set de rute active)
            var (graph, allRoutes, allStations) = await GetOrBuildGraphAsync(activeRouteIds);

            if (graph.Nodes.Count == 0)
            {
                return alternativeRoutes;
            }

            // 1. Ruta optimă (minimizare timp total)
            var optimalPath = DijkstraSearch(graph, startStationId, endStationId, 
                transferPenalty: TRANSFER_PENALTY_MINUTES);
            if (optimalPath != null)
            {
                var route = BuildCalculatedRoute(optimalPath, allRoutes, allStations);
                route.RouteRank = 1;
                route.RouteCategory = "Cea mai rapidă";
                alternativeRoutes.Add(route);
            }

            // 2. Ruta cu penalitate mare pentru transferuri (favorează direct)
            var directPath = DijkstraSearch(graph, startStationId, endStationId, 
                transferPenalty: TRANSFER_PENALTY_MINUTES * 3);
            if (directPath != null && !PathsAreEquivalent(optimalPath, directPath))
            {
                var route = BuildCalculatedRoute(directPath, allRoutes, allStations);
                route.RouteRank = 2;
                route.RouteCategory = "Mai puține transferuri";
                alternativeRoutes.Add(route);
            }

            // 3. Ruta cu penalitate mică pentru walking (permite mai mult walking)
            var walkingPath = DijkstraSearch(graph, startStationId, endStationId, 
                transferPenalty: TRANSFER_PENALTY_MINUTES, 
                maxWalkingDistance: MAX_WALKING_DISTANCE_KM * 1.5);
            if (walkingPath != null && !PathsAreEquivalent(optimalPath, walkingPath) 
                && !PathsAreEquivalent(directPath, walkingPath))
            {
                var route = BuildCalculatedRoute(walkingPath, allRoutes, allStations);
                route.RouteRank = 3;
                route.RouteCategory = "Rută alternativă";
                alternativeRoutes.Add(route);
            }

            // Păstrează doar rute cu max 2 transferuri (3 segmente bus) și sortează după durată
            alternativeRoutes = alternativeRoutes
                .Where(r => r.Segments.Count(s => s.Type == "bus") <= 3)
                .OrderBy(r => r.TotalDuration)
                .Take(3)
                .ToList();

            // Re-numerotează ranking-ul
            for (int i = 0; i < alternativeRoutes.Count; i++)
            {
                alternativeRoutes[i].RouteRank = i + 1;
            }

            // Stamp timestamps pe fiecare rută în funcție de modul de căutare.
            // arriveBy are prioritate peste departAt; ambele absente => pleacă acum.
            foreach (var route in alternativeRoutes)
            {
                var totalSpan = ComputeRouteSpan(route);
                DateTime effectiveDeparture;
                if (arrivalTime.HasValue)
                    effectiveDeparture = arrivalTime.Value.AddMinutes(-totalSpan);
                else if (departureTime.HasValue)
                    effectiveDeparture = departureTime.Value;
                else
                    effectiveDeparture = DateTime.Now;

                StampSegmentTimes(route, effectiveDeparture);
            }

            return alternativeRoutes;
        }

        /// <summary>
        /// Calculează durata totală a unei rute în minute, incluzând penalitățile
        /// de transfer între segmente consecutive de bus pe linii diferite.
        /// </summary>
        private static int ComputeRouteSpan(CalculatedRoute route)
        {
            var span = 0;
            for (int i = 0; i < route.Segments.Count; i++)
            {
                span += route.Segments[i].Duration;
                if (i < route.Segments.Count - 1
                    && route.Segments[i].Type == "bus"
                    && route.Segments[i + 1].Type == "bus"
                    && route.Segments[i].RouteId != route.Segments[i + 1].RouteId)
                {
                    span += TRANSFER_PENALTY_MINUTES;
                }
            }
            return span;
        }

        /// <summary>
        /// Setează DepartureTime/ArrivalTime pe rută și StartTime/EndTime pe fiecare segment.
        /// Între segmente de bus pe linii diferite se inserează gap-ul de transfer,
        /// astfel încât EndTime-ul ultimului segment să coincidă cu ArrivalTime al rutei.
        /// </summary>
        private static void StampSegmentTimes(CalculatedRoute route, DateTime effectiveDeparture)
        {
            route.DepartureTime = effectiveDeparture;
            var cursor = effectiveDeparture;
            for (int i = 0; i < route.Segments.Count; i++)
            {
                var segment = route.Segments[i];
                segment.StartTime = cursor;
                cursor = cursor.AddMinutes(segment.Duration);
                segment.EndTime = cursor;

                if (i < route.Segments.Count - 1
                    && segment.Type == "bus"
                    && route.Segments[i + 1].Type == "bus"
                    && segment.RouteId != route.Segments[i + 1].RouteId)
                {
                    cursor = cursor.AddMinutes(TRANSFER_PENALTY_MINUTES);
                }
            }
            route.ArrivalTime = cursor;
        }

        #endregion

        #region Dijkstra Algorithm Implementation

        // Encodează starea (stationId, routeId) ca long unic.
        // routeId = 0 înseamnă "nu suntem încă pe niciun autobuz" (starea de start).
        private static long EncodeState(int stationId, int routeId) =>
            (long)stationId * 10_000_000L + routeId;

        /// <summary>
        /// Dijkstra cu stare (stație, linie) pentru a penaliza corect transferurile.
        /// Fiecare schimbare de linie la o stație adaugă transferPenalty minute.
        /// </summary>
        private List<GraphNode>? DijkstraSearch(
            TransportGraph graph,
            int startStationId,
            int endStationId,
            int transferPenalty = TRANSFER_PENALTY_MINUTES,
            double maxWalkingDistance = MAX_WALKING_DISTANCE_KM)
        {
            var distances    = new Dictionary<long, double>();
            var predecessors = new Dictionary<long, (int stationId, int routeId, GraphEdge edge)>();
            var visited      = new HashSet<long>();
            var queue        = new PriorityQueue<long, double>();

            // Starea inițială: suntem la stația de start, pe nicio linie (routeId=0)
            var startState = EncodeState(startStationId, 0);
            distances[startState] = 0;
            queue.Enqueue(startState, 0);

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                if (visited.Contains(state)) continue;
                visited.Add(state);

                var currentStationId = (int)(state / 10_000_000L);
                var currentRouteId   = (int)(state % 10_000_000L);

                // Am ajuns la destinație
                if (currentStationId == endStationId)
                    return ReconstructPath(predecessors, startStationId, endStationId, state, graph);

                if (!graph.Nodes.ContainsKey(currentStationId)) continue;

                foreach (var edge in graph.Nodes[currentStationId].Edges)
                {
                    if (edge.Type == EdgeType.Walking)
                    {
                        // Walking edge: resetăm contextul rutei (0 = nu suntem pe nicio linie)
                        if (edge.Distance > maxWalkingDistance) continue;
                        long walkNeighborState = EncodeState(edge.ToStationId, 0);
                        if (visited.Contains(walkNeighborState)) continue;
                        double walkNewDist = distances.GetValueOrDefault(state, double.MaxValue) + edge.TravelTime;
                        if (walkNewDist < distances.GetValueOrDefault(walkNeighborState, double.MaxValue))
                        {
                            distances[walkNeighborState] = walkNewDist;
                            predecessors[walkNeighborState] = (currentStationId, currentRouteId, edge);
                            queue.Enqueue(walkNeighborState, walkNewDist);
                        }
                        continue;
                    }

                    if (edge.Type != EdgeType.Bus) continue;

                    int edgeRouteId   = edge.RouteId ?? 0;
                    int neighborId    = edge.ToStationId;
                    var neighborState = EncodeState(neighborId, edgeRouteId);

                    if (visited.Contains(neighborState)) continue;

                    // Penalitate pentru schimbarea liniei (transfer)
                    double cost = edge.TravelTime;
                    if (currentRouteId != 0 && currentRouteId != edgeRouteId)
                        cost += transferPenalty;

                    var newDist = distances.GetValueOrDefault(state, double.MaxValue) + cost;
                    if (newDist < distances.GetValueOrDefault(neighborState, double.MaxValue))
                    {
                        distances[neighborState] = newDist;
                        predecessors[neighborState] = (currentStationId, currentRouteId, edge);
                        queue.Enqueue(neighborState, newDist);
                    }
                }
            }

            return null;
        }

        private List<GraphNode> ReconstructPath(
            Dictionary<long, (int stationId, int routeId, GraphEdge edge)> predecessors,
            int startStationId,
            int endStationId,
            long endState,
            TransportGraph graph)
        {
            var path = new List<(int stationId, GraphEdge? edge)>();
            var current = endState;

            while (true)
            {
                var stationId = (int)(current / 10_000_000L);
                if (stationId == startStationId && !predecessors.ContainsKey(current)) break;

                if (!predecessors.ContainsKey(current))
                    return new List<GraphNode>();

                var (prevStationId, prevRouteId, edge) = predecessors[current];
                path.Add((stationId, edge));
                current = EncodeState(prevStationId, prevRouteId);
            }
            path.Add((startStationId, null));
            path.Reverse();

            // Construiește lista de GraphNode cu IncomingEdge setat pe copii
            var result = new List<GraphNode>();
            foreach (var (sid, inEdge) in path)
            {
                if (!graph.Nodes.ContainsKey(sid)) return new List<GraphNode>();
                var orig = graph.Nodes[sid];
                var copy = new GraphNode
                {
                    StationId    = orig.StationId,
                    Station      = orig.Station,
                    Edges        = orig.Edges,
                    IncomingEdge = inEdge
                };
                result.Add(copy);
            }
            return result;
        }

        #endregion

        #region Calendar / Day-of-Week Filtering

        /// <summary>
        /// Interogă tabelele ServiceCalendars + Trips pentru a determina ce rute
        /// circulă într-o anumită zi. Folosește raw SQL deoarece Trips și
        /// ServiceCalendars nu sunt (încă) în EF DbContext.
        /// </summary>
        private async Task<HashSet<int>> GetActiveRouteIdsForDate(DateTime targetDate)
        {
            var activeRouteIds = new HashSet<int>();

            var dayColumn = targetDate.DayOfWeek switch
            {
                DayOfWeek.Monday => "Monday",
                DayOfWeek.Tuesday => "Tuesday",
                DayOfWeek.Wednesday => "Wednesday",
                DayOfWeek.Thursday => "Thursday",
                DayOfWeek.Friday => "Friday",
                DayOfWeek.Saturday => "Saturday",
                DayOfWeek.Sunday => "Sunday",
                _ => "Monday"
            };

            var connString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connString))
                return activeRouteIds;

            using var conn = new SqliteConnection(connString);
            await conn.OpenAsync();

            // Verifică dacă tabelul ServiceCalendars există
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ServiceCalendars'";
                var tableExists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) > 0;
                if (!tableExists)
                    return activeRouteIds;
            }

            // Verifică dacă tabelul are date
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = "SELECT COUNT(*) FROM ServiceCalendars";
                var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
                if (count == 0)
                    return activeRouteIds;
            }

            // Query: găsește RouteId-urile care au cel puțin un Trip cu ServiceId
            // activ în ziua selectată și în intervalul de date valid
            var sql = $@"
                SELECT DISTINCT t.RouteId
                FROM Trips t
                INNER JOIN ServiceCalendars sc ON sc.ServiceId = t.ServiceId
                WHERE sc.{dayColumn} = 1
                  AND @TargetDate >= sc.StartDate
                  AND @TargetDate <= sc.EndDate";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new SqliteParameter("@TargetDate", targetDate.ToString("yyyy-MM-dd")));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                activeRouteIds.Add(reader.GetInt32(0));
            }

            return activeRouteIds;
        }

        #endregion

        #region Graph Construction

        /// <summary>
        /// Construiește graful de transport din datele despre rute și stații.
        /// Graful include muchii pentru:
        /// - Călătorii pe același traseu de autobuz
        /// - Transferuri între trasee diferite la aceeași stație
        /// - Walking între stații apropiate (sub MAX_WALKING_DISTANCE_KM)
        /// </summary>
        private TransportGraph BuildTransportGraph(List<RouteModel> routes, List<Station> stations, HashSet<int>? activeRouteIds = null)
        {
            var graph = new TransportGraph();

            // 1. Creează noduri pentru toate stațiile
            foreach (var station in stations)
            {
                graph.Nodes[station.Id] = new GraphNode
                {
                    StationId = station.Id,
                    Station = station,
                    Edges = new List<GraphEdge>()
                };
            }

            // 2. Adaugă muchii pentru fiecare traseu de autobuz
            foreach (var route in routes)
            {
                // Skip rutele care nu circulă în ziua selectată
                if (activeRouteIds != null && !activeRouteIds.Contains(route.Id))
                    continue;

                var routeStations = route.RouteStations
                    .OrderBy(rs => rs.Order)
                    .ToList();

                // Pentru fiecare pereche consecutivă de stații pe traseu
                for (int i = 0; i < routeStations.Count - 1; i++)
                {
                    var fromStation = routeStations[i].Station!;
                    var toStation = routeStations[i + 1].Station!;

                    var distance = CalculateDistance(
                        fromStation.Latitude, fromStation.Longitude,
                        toStation.Latitude, toStation.Longitude);

                    var travelTime = (distance / AVG_BUS_SPEED_KM_PER_HOUR) * 60 + STATION_STOP_TIME_MINUTES;

                    graph.Nodes[fromStation.Id].Edges.Add(new GraphEdge
                    {
                        FromStationId = fromStation.Id,
                        ToStationId = toStation.Id,
                        RouteId = route.Id,
                        RouteNumber = route.RouteNumber,
                        Distance = distance,
                        TravelTime = travelTime,
                        Type = EdgeType.Bus
                    });
                }
            }

            // 3. Transferuri la stații comune între trasee diferite.
            // La o stație servită de mai multe linii, Dijkstra poate comuta
            // linia în mod natural prin muchiile Bus. Adăugăm o muchie explicită
            // de transfer (cu penalitate) pentru a modela timpul de așteptare.
            // Folosim noduri virtuale per-rută pentru a evita blocarea prin visited-set.
            var stationToRoutes = new Dictionary<int, List<RouteModel>>();
            foreach (var route in routes)
            {
                foreach (var rs in route.RouteStations)
                {
                    if (!stationToRoutes.ContainsKey(rs.StationId))
                        stationToRoutes[rs.StationId] = new List<RouteModel>();
                    if (!stationToRoutes[rs.StationId].Any(r => r.Id == route.Id))
                        stationToRoutes[rs.StationId].Add(route);
                }
            }
            // Stațiile cu 2+ trasee sunt puncte de transfer. Dijkstra va putea
            // continua pe orice linie disponibilă la acea stație fără muchie explicită,
            // deoarece fiecare linie are propriile muchii Bus care pleacă din nod.

            // 4. Adaugă muchii de walking între stații apropiate
            AddWalkingEdges(graph, stations);

            return graph;
        }

        /// <summary>
        /// Adaugă muchii de walking între stații aflate la distanță mică.
        /// Permite planificarea rutelor care includ mers pe jos între stații.
        /// </summary>
        private void AddWalkingEdges(TransportGraph graph, List<Station> stations)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                for (int j = i + 1; j < stations.Count; j++)
                {
                    var station1 = stations[i];
                    var station2 = stations[j];

                    var distance = CalculateDistance(
                        station1.Latitude, station1.Longitude,
                        station2.Latitude, station2.Longitude);

                    // Doar dacă distanța este sub pragul maxim
                    if (distance <= MAX_WALKING_DISTANCE_KM)
                    {
                        var walkingTime = (distance / WALKING_SPEED_KM_PER_HOUR) * 60;

                        // Muchie bidirecțională
                        graph.Nodes[station1.Id].Edges.Add(new GraphEdge
                        {
                            FromStationId = station1.Id,
                            ToStationId = station2.Id,
                            Distance = distance,
                            TravelTime = walkingTime,
                            Type = EdgeType.Walking
                        });

                        graph.Nodes[station2.Id].Edges.Add(new GraphEdge
                        {
                            FromStationId = station2.Id,
                            ToStationId = station1.Id,
                            Distance = distance,
                            TravelTime = walkingTime,
                            Type = EdgeType.Walking
                        });
                    }
                }
            }
        }

        #endregion

        #region Path Processing and Route Building

        /// <summary>
        /// Construiește un CalculatedRoute din path-ul returnat de Dijkstra.
        /// Grupează nodurile consecutive pe același traseu în segmente.
        /// </summary>
        private CalculatedRoute BuildCalculatedRoute(
            List<GraphNode> path, 
            List<RouteModel> allRoutes, 
            List<Station> allStations)
        {
            var segments = new List<RouteSegment>();
            var totalDuration = 0.0;

            int i = 0;
            while (i < path.Count - 1)
            {
                var currentNode = path[i];
                var edge = path[i + 1].IncomingEdge;

                if (edge == null)
                {
                    i++;
                    continue;
                }

                if (edge.Type == EdgeType.Walking)
                {
                    segments.Add(new RouteSegment
                    {
                        Type = "walk",
                        StartStation = currentNode.Station,
                        EndStation = path[i + 1].Station,
                        Duration = (int)Math.Ceiling(edge.TravelTime),
                        Distance = edge.Distance
                    });
                    totalDuration += edge.TravelTime;
                    i++;
                    continue;
                }

                if (edge.Type == EdgeType.Bus)
                {
                    var routeId = edge.RouteId;
                    // Stația curentă este și stația de urcare (boarding / transfer)
                    var segmentNodes = new List<GraphNode> { currentNode };
                    var segmentDuration = 0.0;

                    while (i < path.Count - 1)
                    {
                        var nextEdge = path[i + 1].IncomingEdge;
                        if (nextEdge == null || nextEdge.Type != EdgeType.Bus || nextEdge.RouteId != routeId)
                            break;

                        segmentNodes.Add(path[i + 1]);
                        segmentDuration += nextEdge.TravelTime;
                        i++;
                    }

                    var route = allRoutes.FirstOrDefault(r => r.Id == routeId);
                    if (route == null)
                    {
                        _logger.LogWarning("Route with ID {RouteId} not found in allRoutes during path building — skipping segment", routeId);
                        i++;
                        continue;
                    }
                    segments.Add(new RouteSegment
                    {
                        Type = "bus",
                        RouteId = route.Id,
                        RouteNumber = route.RouteNumber,
                        RouteName = route.Name,
                        Color = route.Color,
                        StartStation = segmentNodes.First().Station,
                        EndStation = segmentNodes.Last().Station,
                        Duration = (int)Math.Ceiling(segmentDuration),
                        StationCount = segmentNodes.Count
                    });

                    totalDuration += segmentDuration;

                    // Adaugă penalitate de transfer la durata totală dacă urmează un alt segment de bus
                    if (i < path.Count - 1)
                    {
                        var lookAhead = path[i + 1].IncomingEdge;
                        if (lookAhead?.Type == EdgeType.Bus && lookAhead.RouteId != routeId)
                            totalDuration += TRANSFER_PENALTY_MINUTES;
                    }

                    // NU incrementăm i — stația curentă (i) devine stația de urcare
                    // pentru segmentul următor (dacă există transfer).
                    continue;
                }

                i++;
            }

            var busCount = segments.Count(s => s.Type == "bus");
            var routeType = busCount <= 1 ? "direct" : "transfer";

            return new CalculatedRoute
            {
                RouteType = routeType,
                TotalDuration = (int)Math.Ceiling(totalDuration),
                Segments = segments
            };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Verifică dacă două path-uri sunt echivalente (trec prin aceleași stații principale).
        /// </summary>
        private bool PathsAreEquivalent(List<GraphNode>? path1, List<GraphNode>? path2)
        {
            if (path1 == null || path2 == null)
                return false;

            if (path1.Count != path2.Count)
                return false;

            for (int i = 0; i < path1.Count; i++)
            {
                if (path1[i].StationId != path2[i].StationId)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Calculează distanța Haversine între două coordonate GPS.
        /// Returnează distanța în kilometri.
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Raza Pământului în km

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        #endregion

        #region Schedule-Based Planner (time-dependent Dijkstra peste StopTimes)

        // ── Structuri de date pentru indexul de orar ───────────────────────────

        /// <summary>O oprire dintr-o cursă: stația, secunda de sosire și de plecare.</summary>
        private record ScheduleStop(int StopId, int ArrSec, int DepSec);

        /// <summary>O cursă reală (un Trip GTFS) cu opririle ei în ordine.</summary>
        private sealed class ScheduleTrip
        {
            public int RouteId { get; init; }
            public int DirectionId { get; init; }
            public List<ScheduleStop> Stops { get; init; } = new();
        }

        /// <summary>O plecare reală dintr-o stație (pentru indexul per-stație).</summary>
        private record Departure(int DepSec, int TripIndex, int StopIndex);

        /// <summary>Muchie de mers pe jos între două stații apropiate.</summary>
        private record WalkLink(int ToStationId, int WalkSec, double DistKm);

        /// <summary>
        /// Indexul complet de orar pentru o anumită zi: cursele active, plecările
        /// sortate per stație, legăturile pietonale și dicționarele de rute/stații.
        /// </summary>
        private sealed class ScheduleIndex
        {
            public List<ScheduleTrip> Trips { get; init; } = new();
            // stationId -> plecările care pleacă din ea, sortate crescător după DepSec
            public Dictionary<int, List<Departure>> DeparturesByStation { get; init; } = new();
            // stationId -> stații accesibile pe jos
            public Dictionary<int, List<WalkLink>> WalkLinks { get; init; } = new();
            public Dictionary<int, Station> Stations { get; init; } = new();
            public Dictionary<int, RouteModel> Routes { get; init; } = new();
        }

        private record ScheduleCacheEntry(ScheduleIndex? Index);

        // ── Construcția / cache-ul indexului ──────────────────────────────────

        /// <summary>
        /// Construiește (sau ia din cache) indexul de orar pentru ziua dată.
        /// Returnează null dacă datele de orar nu sunt disponibile (ex. EF in-memory,
        /// tabelele GTFS lipsesc), caz în care apelantul face fallback la estimativ.
        /// </summary>
        private async Task<ScheduleIndex?> GetOrBuildScheduleIndexAsync(DateTime targetDate)
        {
            var cacheKey = $"schedule_index_{targetDate:yyyy-MM-dd}";
            if (_cache.TryGetValue(cacheKey, out ScheduleCacheEntry? cached) && cached != null)
                return cached.Index;

            var index = await BuildScheduleIndexAsync(targetDate);
            _cache.Set(cacheKey, new ScheduleCacheEntry(index), TimeSpan.FromMinutes(5));
            return index;
        }

        private async Task<ScheduleIndex?> BuildScheduleIndexAsync(DateTime targetDate)
        {
            var connString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connString))
                return null; // ex. provider in-memory din teste -> fallback estimativ

            var activeServiceIds = await GetActiveServiceIdsForDate(targetDate, connString);
            if (activeServiceIds.Count == 0)
                return null;

            var trips = new List<ScheduleTrip>();
            var tripIndexByTripId = new Dictionary<string, int>();

            using (var conn = new SqliteConnection(connString))
            {
                await conn.OpenAsync();

                // Tabelele StopTimes/Trips trebuie să existe
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText =
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('StopTimes','Trips')";
                    if (Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) < 2)
                        return null;
                }

                // Aducem toate opririle curselor active, în ordinea (TripId, StopSequence).
                // Filtrăm pe ServiceId-urile active în memorie pentru a evita un IN gigant.
                var sql = @"
                    SELECT t.TripId, t.RouteId, COALESCE(t.DirectionId, 0) AS DirectionId,
                           t.ServiceId, st.StopId, st.ArrivalTime, st.DepartureTime
                    FROM Trips t
                    INNER JOIN StopTimes st ON st.TripId = t.TripId
                    ORDER BY t.TripId, st.StopSequence";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var serviceId = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    if (!activeServiceIds.Contains(serviceId))
                        continue;

                    var tripId = reader.GetString(0);
                    if (!tripIndexByTripId.TryGetValue(tripId, out var idx))
                    {
                        idx = trips.Count;
                        tripIndexByTripId[tripId] = idx;
                        trips.Add(new ScheduleTrip
                        {
                            RouteId = reader.GetInt32(1),
                            DirectionId = reader.GetInt32(2)
                        });
                    }

                    var arrSec = ParseGtfsTime(reader.GetString(5));
                    var depSec = ParseGtfsTime(reader.GetString(6));
                    if (arrSec < 0 || depSec < 0) continue;

                    trips[idx].Stops.Add(new ScheduleStop(reader.GetInt32(4), arrSec, depSec));
                }
            }

            if (trips.Count == 0)
                return null;

            // Dicționare de stații/rute din EF (au coordonate, nume, culori)
            var stations = await _context.Stations.AsNoTracking().ToListAsync();
            var routes = await _context.Routes.AsNoTracking().ToListAsync();
            var stationDict = stations.ToDictionary(s => s.Id);
            var routeDict = routes.ToDictionary(r => r.Id);

            // Index de plecări per stație (toate opririle în afară de ultima din fiecare cursă)
            var departuresByStation = new Dictionary<int, List<Departure>>();
            for (int ti = 0; ti < trips.Count; ti++)
            {
                var stops = trips[ti].Stops;
                for (int si = 0; si < stops.Count - 1; si++)
                {
                    var stopId = stops[si].StopId;
                    if (!departuresByStation.TryGetValue(stopId, out var list))
                    {
                        list = new List<Departure>();
                        departuresByStation[stopId] = list;
                    }
                    list.Add(new Departure(stops[si].DepSec, ti, si));
                }
            }
            foreach (var list in departuresByStation.Values)
                list.Sort((a, b) => a.DepSec.CompareTo(b.DepSec));

            // Legături pietonale între stații apropiate (sub pragul de walking)
            var walkLinks = BuildWalkLinks(stations);

            return new ScheduleIndex
            {
                Trips = trips,
                DeparturesByStation = departuresByStation,
                WalkLinks = walkLinks,
                Stations = stationDict,
                Routes = routeDict
            };
        }

        /// <summary>
        /// Determină ServiceId-urile active într-o anumită zi (din ServiceCalendars).
        /// </summary>
        private async Task<HashSet<string>> GetActiveServiceIdsForDate(DateTime targetDate, string connString)
        {
            var result = new HashSet<string>();

            var dayColumn = targetDate.DayOfWeek switch
            {
                DayOfWeek.Monday => "Monday",
                DayOfWeek.Tuesday => "Tuesday",
                DayOfWeek.Wednesday => "Wednesday",
                DayOfWeek.Thursday => "Thursday",
                DayOfWeek.Friday => "Friday",
                DayOfWeek.Saturday => "Saturday",
                DayOfWeek.Sunday => "Sunday",
                _ => "Monday"
            };

            using var conn = new SqliteConnection(connString);
            await conn.OpenAsync();

            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ServiceCalendars'";
                if (Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) == 0)
                    return result;
            }

            var sql = $@"
                SELECT ServiceId FROM ServiceCalendars
                WHERE {dayColumn} = 1
                  AND @TargetDate >= StartDate
                  AND @TargetDate <= EndDate";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new SqliteParameter("@TargetDate", targetDate.ToString("yyyy-MM-dd")));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(reader.GetString(0));

            return result;
        }

        /// <summary>Parsează o oră GTFS "HH:MM:SS" (poate depăși 24h) în secunde de la miezul nopții.</summary>
        private static int ParseGtfsTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return -1;
            var parts = value.Split(':');
            if (parts.Length < 2) return -1;
            if (!int.TryParse(parts[0], out var h)) return -1;
            if (!int.TryParse(parts[1], out var m)) return -1;
            var s = 0;
            if (parts.Length >= 3 && !int.TryParse(parts[2], out s)) s = 0;
            return h * 3600 + m * 60 + s;
        }

        private Dictionary<int, List<WalkLink>> BuildWalkLinks(List<Station> stations)
        {
            var links = new Dictionary<int, List<WalkLink>>();
            for (int i = 0; i < stations.Count; i++)
            {
                for (int j = i + 1; j < stations.Count; j++)
                {
                    var a = stations[i];
                    var b = stations[j];
                    var dist = CalculateDistance(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                    if (dist > MAX_WALKING_DISTANCE_KM) continue;

                    var walkSec = (int)Math.Round(dist / WALKING_SPEED_KM_PER_HOUR * 3600);

                    if (!links.TryGetValue(a.Id, out var la)) { la = new(); links[a.Id] = la; }
                    if (!links.TryGetValue(b.Id, out var lb)) { lb = new(); links[b.Id] = lb; }
                    la.Add(new WalkLink(b.Id, walkSec, dist));
                    lb.Add(new WalkLink(a.Id, walkSec, dist));
                }
            }
            return links;
        }

        // ── Algoritmul de planificare (earliest arrival + enumerare) ──────────

        /// <summary>Cum am ajuns la o stație în căutarea pe orar.</summary>
        private enum ArrivalKind { Start, Walk, Transit }

        /// <summary>Un picior al călătoriei (segment de bus sau de mers pe jos).</summary>
        private sealed class JourneyLeg
        {
            public bool IsWalk { get; init; }
            public int FromStationId { get; init; }
            public int ToStationId { get; init; }
            public int DepSec { get; init; }
            public int ArrSec { get; init; }
            public int RouteId { get; init; }
            public int StationCount { get; init; }
            public double DistKm { get; init; }
        }

        private sealed class Journey
        {
            public List<JourneyLeg> Legs { get; init; } = new();
            public int DepartSec => Legs.Count > 0 ? Legs[0].DepSec : 0;
            public int ArriveSec => Legs.Count > 0 ? Legs[^1].ArrSec : 0;
            // Secunda primei urcări în vehicul (pentru a enumera plecarea următoare)
            public int FirstBoardSec
            {
                get
                {
                    foreach (var leg in Legs)
                        if (!leg.IsWalk) return leg.DepSec;
                    return -1;
                }
            }
        }

        /// <summary>
        /// Construiește rezultatele finale (CalculatedRoute) pentru cererea dată,
        /// folosind orarul real. Tratează atât "departAt" cât și "arriveBy".
        /// </summary>
        private List<CalculatedRoute> PlanScheduleBasedRoutes(
            ScheduleIndex index,
            int startStationId,
            int endStationId,
            DateTime? departureTime,
            DateTime? arrivalTime,
            DateTime effectiveDate)
        {
            if (!index.Stations.ContainsKey(startStationId) || !index.Stations.ContainsKey(endStationId))
                return new List<CalculatedRoute>();

            var baseDate = effectiveDate.Date;
            List<Journey> journeys;

            if (arrivalTime.HasValue)
            {
                var arriveBySec = (int)(arrivalTime.Value - arrivalTime.Value.Date).TotalSeconds;
                journeys = EnumerateArriveBy(index, startStationId, endStationId, arriveBySec);
            }
            else
            {
                var departReq = departureTime ?? DateTime.Now;
                var departSec = (int)(departReq - departReq.Date).TotalSeconds;
                journeys = EnumerateDepartAt(index, startStationId, endStationId, departSec, MAX_SCHEDULE_ALTERNATIVES);
            }

            var result = new List<CalculatedRoute>();
            foreach (var journey in journeys)
            {
                var route = ConvertJourney(journey, index, baseDate);
                result.Add(route);
            }

            // Rank + categorii (prima = cea mai rapidă/devreme)
            for (int i = 0; i < result.Count; i++)
            {
                result[i].RouteRank = i + 1;
                result[i].RouteCategory = i == 0
                    ? "Cea mai rapidă"
                    : $"Plecare la {result[i].DepartureTime:HH:mm}";
            }

            return result;
        }

        /// <summary>
        /// Enumeră plecările reale succesive (gen lista Google): prima opțiune e
        /// sosirea cea mai devreme plecând la/după ora cerută, apoi se avansează
        /// la cursa următoare și se reia, până la maxResults opțiuni distincte.
        /// </summary>
        private List<Journey> EnumerateDepartAt(
            ScheduleIndex index, int start, int end, int fromSec, int maxResults)
        {
            var journeys = new List<Journey>();
            var seen = new HashSet<string>();
            var cursor = fromSec;
            var limit = fromSec + MAX_SCHEDULE_SEARCH_SECONDS;

            for (int guard = 0; guard < maxResults * 6 && journeys.Count < maxResults; guard++)
            {
                var journey = ComputeEarliestArrival(index, start, end, cursor);
                if (journey == null) break;

                var firstBoard = journey.FirstBoardSec;
                var signature = JourneySignature(journey);
                if (seen.Add(signature))
                    journeys.Add(journey);

                if (firstBoard < 0) break;              // doar mers pe jos: nu există "următoarea cursă"
                if (firstBoard >= limit) break;
                cursor = firstBoard + 1;                 // forțează cursa următoare
            }

            return journeys;
        }

        /// <summary>
        /// Pentru "arriveBy": enumeră călătoriile dintr-o fereastră înainte de ora
        /// cerută, păstrează doar cele ce sosesc la timp și le alege pe cele cu
        /// plecarea cea mai târzie (minim de așteptare), sortate după sosire.
        /// </summary>
        private List<Journey> EnumerateArriveBy(
            ScheduleIndex index, int start, int end, int arriveBySec)
        {
            var windowStart = Math.Max(0, arriveBySec - MAX_SCHEDULE_SEARCH_SECONDS);
            var candidates = EnumerateDepartAt(index, start, end, windowStart, MAX_SCHEDULE_ALTERNATIVES * 6);

            var feasible = candidates
                .Where(j => j.ArriveSec <= arriveBySec)
                .OrderByDescending(j => j.DepartSec)
                .Take(MAX_SCHEDULE_ALTERNATIVES)
                .OrderBy(j => j.ArriveSec)
                .ToList();

            return feasible;
        }

        /// <summary>
        /// Time-dependent Dijkstra: găsește călătoria cu sosire cât mai devreme la
        /// destinație, plecând din stația de start nu mai devreme de earliestDepSec.
        /// Costul fiecărei muchii reflectă timpul real de așteptare + mers al curselor.
        /// </summary>
        private Journey? ComputeEarliestArrival(
            ScheduleIndex index, int start, int end, int earliestDepSec)
        {
            // earliest known arrival time (sec) per stație
            var best = new Dictionary<int, int> { [start] = earliestDepSec };
            var predecessor = new Dictionary<int, JourneyLeg>();
            var arrivalKind = new Dictionary<int, ArrivalKind> { [start] = ArrivalKind.Start };
            var finalized = new HashSet<int>();
            var pq = new PriorityQueue<int, int>();
            pq.Enqueue(start, earliestDepSec);

            while (pq.Count > 0)
            {
                var station = pq.Dequeue();
                if (finalized.Contains(station)) continue;
                finalized.Add(station);

                var timeHere = best[station];
                if (station == end) break;

                var kindHere = arrivalKind[station];

                // 1. Urcarea în vehicule: pentru fiecare (rută, direcție) servită aici,
                //    luăm cea mai devreme cursă care pleacă după ce putem urca.
                var board = timeHere + (kindHere == ArrivalKind.Transit ? MIN_TRANSFER_SECONDS : 0);
                if (index.DeparturesByStation.TryGetValue(station, out var departures))
                {
                    var startIdx = LowerBoundByDep(departures, board);
                    var takenGroups = new HashSet<long>();
                    for (int di = startIdx; di < departures.Count; di++)
                    {
                        var dep = departures[di];
                        var trip = index.Trips[dep.TripIndex];
                        long groupKey = (long)trip.RouteId * 8 + trip.DirectionId;
                        if (!takenGroups.Add(groupKey)) continue; // deja avem cea mai devreme cursă pe acest grup

                        // Coborâm la fiecare stație ulterioară din cursă
                        for (int sj = dep.StopIndex + 1; sj < trip.Stops.Count; sj++)
                        {
                            var alight = trip.Stops[sj];
                            var arr = alight.ArrSec;
                            if (finalized.Contains(alight.StopId)) continue;
                            if (arr < best.GetValueOrDefault(alight.StopId, int.MaxValue))
                            {
                                best[alight.StopId] = arr;
                                arrivalKind[alight.StopId] = ArrivalKind.Transit;
                                predecessor[alight.StopId] = new JourneyLeg
                                {
                                    IsWalk = false,
                                    FromStationId = station,
                                    ToStationId = alight.StopId,
                                    DepSec = dep.DepSec,
                                    ArrSec = arr,
                                    RouteId = trip.RouteId,
                                    StationCount = sj - dep.StopIndex + 1
                                };
                                pq.Enqueue(alight.StopId, arr);
                            }
                        }
                    }
                }

                // 2. Mers pe jos către stații apropiate — interzis două mersuri la rând,
                //    pentru a nu traversa orașul din salturi de 500m.
                if (kindHere != ArrivalKind.Walk &&
                    index.WalkLinks.TryGetValue(station, out var walks))
                {
                    foreach (var link in walks)
                    {
                        if (finalized.Contains(link.ToStationId)) continue;
                        var arr = timeHere + link.WalkSec;
                        if (arr < best.GetValueOrDefault(link.ToStationId, int.MaxValue))
                        {
                            best[link.ToStationId] = arr;
                            arrivalKind[link.ToStationId] = ArrivalKind.Walk;
                            predecessor[link.ToStationId] = new JourneyLeg
                            {
                                IsWalk = true,
                                FromStationId = station,
                                ToStationId = link.ToStationId,
                                DepSec = timeHere,
                                ArrSec = arr,
                                StationCount = 2,
                                DistKm = link.DistKm
                            };
                            pq.Enqueue(link.ToStationId, arr);
                        }
                    }
                }
            }

            if (!predecessor.ContainsKey(end))
                return null;

            // Reconstruiește călătoria
            var legs = new List<JourneyLeg>();
            var cur = end;
            while (predecessor.TryGetValue(cur, out var leg))
            {
                legs.Add(leg);
                cur = leg.FromStationId;
                if (cur == start) break;
            }
            legs.Reverse();
            return new Journey { Legs = legs };
        }

        /// <summary>Primul index din lista de plecări (sortată după DepSec) cu DepSec &gt;= target.</summary>
        private static int LowerBoundByDep(List<Departure> departures, int target)
        {
            int lo = 0, hi = departures.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (departures[mid].DepSec < target) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        private static string JourneySignature(Journey journey)
        {
            // Identifică o opțiune după liniile folosite + ora primei urcări,
            // ca să nu listăm de două ori aceeași plecare.
            var legs = journey.Legs
                .Select(l => l.IsWalk ? "w" : $"r{l.RouteId}");
            return string.Join(">", legs) + "@" + journey.FirstBoardSec;
        }

        /// <summary>Transformă o Journey internă în CalculatedRoute (forma consumată de frontend).</summary>
        private CalculatedRoute ConvertJourney(Journey journey, ScheduleIndex index, DateTime baseDate)
        {
            var segments = new List<RouteSegment>();

            foreach (var leg in journey.Legs)
            {
                var fromStation = index.Stations.GetValueOrDefault(leg.FromStationId);
                var toStation = index.Stations.GetValueOrDefault(leg.ToStationId);
                var durationMin = (int)Math.Ceiling((leg.ArrSec - leg.DepSec) / 60.0);

                if (leg.IsWalk)
                {
                    segments.Add(new RouteSegment
                    {
                        Type = "walk",
                        StartStation = fromStation,
                        EndStation = toStation,
                        Duration = durationMin,
                        Distance = leg.DistKm,
                        StartTime = baseDate.AddSeconds(leg.DepSec),
                        EndTime = baseDate.AddSeconds(leg.ArrSec)
                    });
                }
                else
                {
                    var route = index.Routes.GetValueOrDefault(leg.RouteId);
                    segments.Add(new RouteSegment
                    {
                        Type = "bus",
                        RouteId = leg.RouteId,
                        RouteNumber = route?.RouteNumber,
                        RouteName = route?.Name,
                        Color = route?.Color,
                        StartStation = fromStation,
                        EndStation = toStation,
                        Duration = durationMin,
                        StationCount = leg.StationCount,
                        StartTime = baseDate.AddSeconds(leg.DepSec),
                        EndTime = baseDate.AddSeconds(leg.ArrSec)
                    });
                }
            }

            var busCount = segments.Count(s => s.Type == "bus");
            return new CalculatedRoute
            {
                RouteType = busCount <= 1 ? "direct" : "transfer",
                TotalDuration = (int)Math.Ceiling((journey.ArriveSec - journey.DepartSec) / 60.0),
                Segments = segments,
                DepartureTime = baseDate.AddSeconds(journey.DepartSec),
                ArrivalTime = baseDate.AddSeconds(journey.ArriveSec)
            };
        }

        #endregion
    }

    #region Graph Data Structures

    /// <summary>
    /// Reprezintă graful complet de transport public.
    /// Fiecare nod = o stație, fiecare muchie = o conexiune (autobuz/walking/transfer).
    /// </summary>
    public class TransportGraph
    {
        public Dictionary<int, GraphNode> Nodes { get; set; } = new();
    }

    /// <summary>
    /// Reprezintă un nod în graf (o stație).
    /// </summary>
    public class GraphNode
    {
        public int StationId { get; set; }
        public Station? Station { get; set; }
        public List<GraphEdge> Edges { get; set; } = new();
        
        /// <summary>
        /// Muchia prin care s-a ajuns la acest nod în Dijkstra (pentru reconstrucția path-ului)
        /// </summary>
        public GraphEdge? IncomingEdge { get; set; }
    }

    /// <summary>
    /// Reprezintă o muchie în graf (o conexiune între stații).
    /// </summary>
    public class GraphEdge
    {
        public int FromStationId { get; set; }
        public int ToStationId { get; set; }
        public int? RouteId { get; set; }
        public string? RouteNumber { get; set; }
        public double Distance { get; set; } // în km
        public double TravelTime { get; set; } // în minute
        public EdgeType Type { get; set; }
    }

    /// <summary>
    /// Tipul muchiei în graf.
    /// </summary>
    public enum EdgeType
    {
        /// <summary>Călătorie cu autobuzul pe un traseu</summary>
        Bus,
        
        /// <summary>Transfer între trasee diferite la aceeași stație</summary>
        Transfer,
        
        /// <summary>Mers pe jos între stații apropiate</summary>
        Walking
    }

    #endregion

    #region Response Models

    /// <summary>
    /// Reprezintă o rută calculată între două stații.
    /// </summary>
    public class CalculatedRoute
    {
        /// <summary>Tipul rutei: "direct", "transfer", "multi-segment"</summary>
        public string RouteType { get; set; } = "direct";
        
        /// <summary>Durata totală în minute</summary>
        public int TotalDuration { get; set; }
        
        /// <summary>Lista de segmente care compun ruta</summary>
        public List<RouteSegment> Segments { get; set; } = new();
        
        /// <summary>Ranking-ul rutei (1 = cea mai bună, 2 = a doua, etc.)</summary>
        public int RouteRank { get; set; }
        
        /// <summary>Categoria rutei: "Cea mai rapidă", "Mai puține transferuri", etc.</summary>
        public string RouteCategory { get; set; } = "";

        /// <summary>Ora de plecare efectivă (pentru afișare în UI)</summary>
        public DateTime DepartureTime { get; set; }

        /// <summary>Ora de sosire estimată (DepartureTime + TotalDuration)</summary>
        public DateTime ArrivalTime { get; set; }
    }

    /// <summary>
    /// Reprezintă un segment al unei rute (călătorie cu autobuzul, transfer, sau walking).
    /// </summary>
    public class RouteSegment
    {
        /// <summary>Tipul segmentului: "bus", "walk", "transfer"</summary>
        public string Type { get; set; } = "bus";

        /// <summary>ID-ul traseului (pentru segmente de tip "bus")</summary>
        public int? RouteId { get; set; }

        /// <summary>Numărul traseului (pentru segmente de tip "bus")</summary>
        public string? RouteNumber { get; set; }
        
        /// <summary>Numele traseului (pentru segmente de tip "bus")</summary>
        public string? RouteName { get; set; }
        
        /// <summary>Culoarea traseului (pentru segmente de tip "bus")</summary>
        public string? Color { get; set; }
        
        /// <summary>Stația de start a segmentului</summary>
        public Station? StartStation { get; set; }
        
        /// <summary>Stația de end a segmentului</summary>
        public Station? EndStation { get; set; }
        
        /// <summary>Durata segmentului în minute</summary>
        public int Duration { get; set; }
        
        /// <summary>Numărul de stații parcurse (pentru segmente de tip "bus")</summary>
        public int StationCount { get; set; }
        
        /// <summary>Distanța în km (pentru segmente de tip "walk")</summary>
        public double? Distance { get; set; }

        /// <summary>Ora de start a segmentului (boarding pentru bus, start walking etc.)</summary>
        public DateTime StartTime { get; set; }

        /// <summary>Ora de final a segmentului (alighting pentru bus, end walking etc.)</summary>
        public DateTime EndTime { get; set; }
    }

    #endregion
}
