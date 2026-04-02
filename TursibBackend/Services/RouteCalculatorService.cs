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
        private const string GRAPH_CACHE_KEY = "transport_graph";
        private const int GRAPH_CACHE_MINUTES = 30;
        private const double WALKING_SPEED_KM_PER_HOUR = 5.0;
        private const double MAX_WALKING_DISTANCE_KM = 0.5;
        private const int TRANSFER_PENALTY_MINUTES = 5;
        private const int AVG_BUS_SPEED_KM_PER_HOUR = 25;
        private const int STATION_STOP_TIME_MINUTES = 1;

        public RouteCalculatorService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>
        /// Invalidează cache-ul grafului (apelat după modificări de rute/stații).
        /// </summary>
        public void InvalidateGraphCache()
        {
            _cache.Remove(GRAPH_CACHE_KEY);
        }

        #region Public API Methods

        /// <summary>
        /// Calculează ruta optimă între două stații folosind algoritmul Dijkstra.
        /// </summary>
        public async Task<CalculatedRoute?> CalculateOptimalRoute(
            int startStationId, 
            int endStationId,
            DateTime? departureTime = null)
        {
            var routes = await CalculateAlternativeRoutes(startStationId, endStationId, departureTime);
            return routes.FirstOrDefault();
        }

        /// <summary>
        /// Calculează multiple rute alternative folosind variații ale algoritmului Dijkstra
        /// cu diferite penalități pentru a obține diversitate în rezultate.
        /// </summary>
        public async Task<List<CalculatedRoute>> CalculateAlternativeRoutes(
            int startStationId,
            int endStationId,
            DateTime? departureTime = null)
        {
            var alternativeRoutes = new List<CalculatedRoute>();

            // Obține graful din cache sau construiește-l
            var (graph, allRoutes) = await GetOrBuildGraphAsync();

            if (graph == null || allRoutes.Count == 0)
            {
                return alternativeRoutes;
            }

            // 1. Ruta optimă (minimizare timp total)
            var optimalPath = DijkstraSearch(graph, startStationId, endStationId,
                transferPenalty: TRANSFER_PENALTY_MINUTES);
            if (optimalPath != null)
            {
                var route = BuildCalculatedRoute(optimalPath, allRoutes);
                route.RouteRank = 1;
                route.RouteCategory = "Cea mai rapidă";
                alternativeRoutes.Add(route);
            }

            // 2. Ruta cu penalitate mare pentru transferuri (favorează direct)
            var directPath = DijkstraSearch(graph, startStationId, endStationId,
                transferPenalty: TRANSFER_PENALTY_MINUTES * 3);
            if (directPath != null && !PathsAreEquivalent(optimalPath, directPath))
            {
                var route = BuildCalculatedRoute(directPath, allRoutes);
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
                var route = BuildCalculatedRoute(walkingPath, allRoutes);
                route.RouteRank = 3;
                route.RouteCategory = "Rută alternativă";
                alternativeRoutes.Add(route);
            }

            // Limitează la top 3 și sortează după durată
            alternativeRoutes = alternativeRoutes
                .OrderBy(r => r.TotalDuration)
                .Take(3)
                .ToList();

            // Re-numerotează ranking-ul
            for (int i = 0; i < alternativeRoutes.Count; i++)
            {
                alternativeRoutes[i].RouteRank = i + 1;
            }

            return alternativeRoutes;
        }

        #endregion

        #region Dijkstra Algorithm Implementation

        /// <summary>
        /// Implementare algoritm Dijkstra adaptat pentru rețele de transport public.
        /// Găsește drumul cu costul minim (timp) între două stații.
        /// </summary>
        private List<GraphNode>? DijkstraSearch(
            TransportGraph graph, 
            int startStationId, 
            int endStationId,
            int transferPenalty = TRANSFER_PENALTY_MINUTES,
            double maxWalkingDistance = MAX_WALKING_DISTANCE_KM)
        {
            // Inițializare: distanțe infinite, precedent null
            var distances = new Dictionary<int, double>();
            var predecessors = new Dictionary<int, (GraphNode node, GraphEdge edge)>();
            var visited = new HashSet<int>();
            
            // Priority queue (min-heap) pentru nodurile de explorat
            var queue = new PriorityQueue<int, double>();

            // Inițializare distanțe
            foreach (var nodeId in graph.Nodes.Keys)
            {
                distances[nodeId] = double.MaxValue;
            }
            distances[startStationId] = 0;
            queue.Enqueue(startStationId, 0);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                
                // Dacă am ajuns la destinație, reconstruim path-ul
                if (currentId == endStationId)
                {
                    return ReconstructPath(predecessors, startStationId, endStationId, graph);
                }

                // Skip dacă deja vizitat
                if (visited.Contains(currentId))
                    continue;

                visited.Add(currentId);

                // Explorează vecinii
                if (!graph.Nodes.ContainsKey(currentId))
                    continue;

                var currentNode = graph.Nodes[currentId];

                foreach (var edge in currentNode.Edges)
                {
                    // Skip walking edges care depășesc distanța maximă
                    if (edge.Type == EdgeType.Walking && edge.Distance > maxWalkingDistance)
                        continue;

                    var neighborId = edge.ToStationId;

                    if (visited.Contains(neighborId))
                        continue;

                    // Calculează costul pentru această muchie
                    double edgeCost = edge.TravelTime;

                    // Aplică penalitate pentru transfer
                    if (edge.Type == EdgeType.Transfer)
                    {
                        edgeCost += transferPenalty;
                    }

                    // Calculează distanța totală prin acest drum
                    var newDistance = distances[currentId] + edgeCost;

                    // Actualizează dacă am găsit un drum mai bun
                    if (newDistance < distances[neighborId])
                    {
                        distances[neighborId] = newDistance;
                        predecessors[neighborId] = (currentNode, edge);
                        queue.Enqueue(neighborId, newDistance);
                    }
                }
            }

            // Nu s-a găsit niciun drum
            return null;
        }

        /// <summary>
        /// Reconstruiește path-ul de la start la end folosind dicționarul de predecesori.
        /// </summary>
        private List<GraphNode> ReconstructPath(
            Dictionary<int, (GraphNode node, GraphEdge edge)> predecessors,
            int startStationId,
            int endStationId,
            TransportGraph graph)
        {
            var path = new List<GraphNode>();
            var current = endStationId;

            // Construiește path-ul de la end la start
            while (current != startStationId)
            {
                if (!predecessors.ContainsKey(current))
                    return new List<GraphNode>(); // Path invalid

                var (node, edge) = predecessors[current];
                
                // Adaugă nodul curent cu muchia prin care am ajuns la el
                var currentNode = graph.Nodes[current];
                currentNode.IncomingEdge = edge;
                path.Add(currentNode);

                current = node.StationId;
            }

            // Adaugă nodul de start
            path.Add(graph.Nodes[startStationId]);

            // Inversează pentru a avea path-ul de la start la end
            path.Reverse();

            return path;
        }

        #endregion

        #region Graph Construction

        /// <summary>
        /// Returnează graful din cache sau îl construiește dacă nu există.
        /// </summary>
        private async Task<(TransportGraph? graph, List<RouteModel> routes)> GetOrBuildGraphAsync()
        {
            if (_cache.TryGetValue(GRAPH_CACHE_KEY, out (TransportGraph graph, List<RouteModel> routes) cached))
            {
                return cached;
            }

            var allRoutes = await _context.Routes
                .Include(r => r.RouteStations)
                .ThenInclude(rs => rs.Station)
                .AsNoTracking()
                .ToListAsync();

            var allStations = await _context.Stations
                .AsNoTracking()
                .ToListAsync();

            if (allRoutes.Count == 0 || allStations.Count == 0)
                return (null, allRoutes);

            var graph = BuildTransportGraph(allRoutes, allStations);
            _cache.Set(GRAPH_CACHE_KEY, (graph, allRoutes), TimeSpan.FromMinutes(GRAPH_CACHE_MINUTES));

            return (graph, allRoutes);
        }

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

            // 2. Adaugă muchii de autobuz pentru fiecare traseu
            foreach (var route in routes)
            {
                var routeStations = route.RouteStations
                    .OrderBy(rs => rs.Order)
                    .ToList();

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

            // 3. Adaugă muchii de transfer la stațiile unde se intersectează mai multe trasee
            // Construiește un index: stationId -> lista de trasee care trec prin ea
            var stationToRoutes = new Dictionary<int, List<RouteModel>>();
            foreach (var route in routes)
            {
                foreach (var rs in route.RouteStations)
                {
                    if (!stationToRoutes.TryGetValue(rs.StationId, out var list))
                    {
                        list = new List<RouteModel>();
                        stationToRoutes[rs.StationId] = list;
                    }
                    list.Add(route);
                }
            }

            // Adaugă o muchie de transfer pentru fiecare pereche (routeA, routeB) la aceeași stație
            foreach (var (stationId, routesAtStation) in stationToRoutes)
            {
                if (routesAtStation.Count < 2 || !graph.Nodes.ContainsKey(stationId))
                    continue;

                var addedTransfers = new HashSet<(int, int)>();
                foreach (var routeA in routesAtStation)
                {
                    foreach (var routeB in routesAtStation)
                    {
                        if (routeA.Id == routeB.Id) continue;
                        var key = (Math.Min(routeA.Id, routeB.Id), Math.Max(routeA.Id, routeB.Id));
                        if (!addedTransfers.Add(key)) continue;

                        graph.Nodes[stationId].Edges.Add(new GraphEdge
                        {
                            FromStationId = stationId,
                            ToStationId = stationId,
                            RouteId = routeB.Id,
                            RouteNumber = routeB.RouteNumber,
                            Distance = 0,
                            TravelTime = TRANSFER_PENALTY_MINUTES,
                            Type = EdgeType.Transfer
                        });
                    }
                }
            }

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
            List<RouteModel> allRoutes)
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

                if (edge.Type == EdgeType.Bus)
                {
                    // Găsește toate nodurile consecutive pe același traseu
                    var routeId = edge.RouteId;
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

                    // Creează segment de autobuz
                    var route = allRoutes.First(r => r.Id == routeId);
                    segments.Add(new RouteSegment
                    {
                        Type = "bus",
                        RouteNumber = route.RouteNumber,
                        RouteName = route.Name,
                        Color = route.Color,
                        StartStation = segmentNodes.First().Station,
                        EndStation = segmentNodes.Last().Station,
                        Duration = (int)Math.Ceiling(segmentDuration),
                        StationCount = segmentNodes.Count
                    });

                    totalDuration += segmentDuration;
                }
                else if (edge.Type == EdgeType.Walking)
                {
                    // Segment de walking
                    segments.Add(new RouteSegment
                    {
                        Type = "walk",
                        StartStation = currentNode.Station,
                        EndStation = path[i + 1].Station,
                        Duration = (int)Math.Ceiling(edge.TravelTime),
                        Distance = edge.Distance
                    });

                    totalDuration += edge.TravelTime;
                }
                else if (edge.Type == EdgeType.Transfer)
                {
                    // Segment de transfer
                    segments.Add(new RouteSegment
                    {
                        Type = "transfer",
                        StartStation = currentNode.Station,
                        EndStation = path[i + 1].Station,
                        Duration = (int)Math.Ceiling(edge.TravelTime)
                    });

                    totalDuration += edge.TravelTime;
                }

                i++;
            }

            // Determină tipul rutei
            var routeType = segments.Count == 1 ? "direct" : 
                           segments.Any(s => s.Type == "transfer") ? "transfer" : "multi-segment";

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
    }

    /// <summary>
    /// Reprezintă un segment al unei rute (călătorie cu autobuzul, transfer, sau walking).
    /// </summary>
    public class RouteSegment
    {
        /// <summary>Tipul segmentului: "bus", "walk", "transfer"</summary>
        public string Type { get; set; } = "bus";
        
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
    }

    #endregion
}
