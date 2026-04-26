using TursibBackend.Data;
using TursibBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private const double WALKING_SPEED_KM_PER_HOUR = 5.0; // Viteza medie de mers pe jos
        private const double MAX_WALKING_DISTANCE_KM = 0.5; // Distanța maximă pentru walking (500m)
        private const int TRANSFER_PENALTY_MINUTES = 5; // Penalitate pentru fiecare transfer
        private const int AVG_BUS_SPEED_KM_PER_HOUR = 25; // Viteza medie autobuz în oraș
        private const int STATION_STOP_TIME_MINUTES = 1; // Timp de oprire la fiecare stație

        private record GraphCacheEntry(TransportGraph Graph, List<RouteModel> AllRoutes, List<Station> AllStations);

        public RouteCalculatorService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private async Task<GraphCacheEntry> GetOrBuildGraphAsync()
        {
            if (_cache.TryGetValue("transport_graph", out GraphCacheEntry? cached) && cached != null)
                return cached;

            var allRoutes = await _context.Routes
                .AsNoTracking()
                .Include(r => r.RouteStations)
                .ThenInclude(rs => rs.Station)
                .ToListAsync();
            var allStations = await _context.Stations.AsNoTracking().ToListAsync();

            var graph = BuildTransportGraph(allRoutes, allStations);
            var entry = new GraphCacheEntry(graph, allRoutes, allStations);
            _cache.Set("transport_graph", entry, TimeSpan.FromMinutes(5));
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

            // Load graph and route data (cached for 5 minutes)
            var (graph, allRoutes, allStations) = await GetOrBuildGraphAsync();

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

        #region Graph Construction

        /// <summary>
        /// Construiește graful de transport din datele despre rute și stații.
        /// Graful include muchii pentru:
        /// - Călătorii pe același traseu de autobuz
        /// - Transferuri între trasee diferite la aceeași stație
        /// - Walking între stații apropiate (sub MAX_WALKING_DISTANCE_KM)
        /// </summary>
        private TransportGraph BuildTransportGraph(List<RouteModel> routes, List<Station> stations)
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

                    var route = allRoutes.First(r => r.Id == routeId);
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
