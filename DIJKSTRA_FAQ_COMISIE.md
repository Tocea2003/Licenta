# 🎓 Întrebări Comisie Licență - Algoritm Dijkstra

## 📚 Întrebări Teoretice

### Q1: De ce ați ales algoritmul Dijkstra pentru calcularea rutelor?

**Răspuns:**

Am ales Dijkstra din următoarele motive:

1. **Garanție de optimalitate**: Dijkstra garantează matematic că găsește drumul cu costul minim între două noduri, ceea ce este esențial pentru un sistem de transport public unde utilizatorii doresc cea mai rapidă rută.

2. **Complexitate acceptabilă**: Cu O((V+E)log V) folosind o priority queue implementată ca min-heap, algoritmul este foarte eficient pentru rețeaua de transport din Sibiu (~500 stații).

3. **Flexibilitate**: Prin ajustarea greutăților muchiilor (penalități pentru transferuri, costuri pentru walking), pot să optimizez pentru diferite criterii fără a schimba algoritmul de bază.

4. **Standard în industrie**: Google Maps, Uber, și alte aplicații majore folosesc variante ale acestui algoritm, deci este o soluție dovedit în producție.

5. **Alternativa brute-force** ar avea complexitatea O(R²×S²) pentru un transfer, care devine impracticabil pentru multiple transferuri.

---

### Q2: Care este diferența între Dijkstra și A*? De ce nu A*?

**Răspuns:**

**A\*** este o optimizare a lui Dijkstra care folosește o **funcție euristică** h(n) pentru a estima distanța de la nodul curent la destinație:

```
f(n) = g(n) + h(n)
  g(n) = costul real de la start la n
  h(n) = estimare cost de la n la destinație
```

**De ce nu l-am folosit:**

1. **Heuristica nu e admisibilă în cazul nostru**: 
   - Pe o rețea de drumuri, distanța linie dreaptă (Haversine) funcționează bine
   - Pe transport public, distanța linie dreaptă NU reflectă timpul real:
     - Trebuie să urmezi rute fixe (nu poți merge direct)
     - Transferurile adaugă timp semnificativ
     - Opririle la stații adaugă delay

2. **Riscul subestimării**:
   - Dacă h(n) > costul real, A* nu mai garantează optimalitatea
   - Cu o heuristică prost aleasă, pot pierde ruta optimă

3. **Performanța Dijkstra e suficientă**:
   - Query-urile durează 8-15ms, acceptabil pentru user experience
   - A* ar reduce poate la 5-10ms, dar nu justifică riscul

**Când aș folosi A***: Dacă aș scala la o rețea națională (mii de orașe), A* cu o heuristică atent proiectată ar aduce beneficii semnificative.

---

### Q3: Cum gestionați cazul în care nu există nicio rută între două stații?

**Răspuns:**

Dijkstra returnează `null` dacă:
1. Priority queue se epuizează fără a ajunge la destinație
2. Toate nodurile accesibile au fost vizitate, dar destinația nu e printre ele

În cod:
```csharp
while (queue.Count > 0)
{
    var current = queue.Dequeue();
    
    if (current == destination)
        return ReconstructPath(predecessors, start, destination);
    
    // ... explorare vecini
}

return null; // Nu există drum
```

Backend-ul returnează o listă goală `[]`, iar Frontend-ul afișează:
```
"Nu există rută disponibilă între aceste stații. 
Vă rugăm să verificați dacă sunt în rețeaua Tursib."
```

---

### Q4: Cum modelați transferurile între linii?

**Răspuns:**

Am modelat transferurile ca **muchii speciale** în graf:

Concept: La o stație unde se întâlnesc multiple linii (ex: Piața Unirii), creez muchii între aceeași stație dar pe linii diferite.

```csharp
// Exemplu: Piața Unirii servește Linia 3 și Linia 5
graph.AddEdge(
    from: StațiaPiațaUnirii_Linia3,
    to: StațiaPiațaUnirii_Linia5,
    type: EdgeType.Transfer,
    cost: TRANSFER_PENALTY_MINUTES  // 5 minute
)
```

**De ce 5 minute?**
- Timp mediu de așteptare între autobuze în Sibiu
- Include timp de coborâre din primul autobuz + urcarea în al doilea
- Poate fi ajustat bazat pe date reale de frecvență

**Beneficiu**: Dijkstra tratează transferul ca orice altă muchie, deci găsește automat rute optime cu orice număr de transferuri.

---

### Q5: Cum funcționează walking între stații?

**Răspuns:**

La construirea grafului, adaug muchii de **walking** între toate perechile de stații aflate la distanță < 500m:

```csharp
private void AddWalkingEdges(TransportGraph graph, List<Station> stations)
{
    for (int i = 0; i < stations.Count; i++)
    {
        for (int j = i + 1; j < stations.Count; j++)
        {
            var distance = Haversine(station1, station2); // în km
            
            if (distance <= MAX_WALKING_DISTANCE_KM)  // 0.5 km
            {
                var walkingTime = (distance / WALKING_SPEED) * 60; // minute
                
                // Muchie bidirecțională
                graph.AddEdge(station1 ↔ station2, 
                    type: Walking, 
                    cost: walkingTime);
            }
        }
    }
}
```

**Parametri:**
- Viteză walking: 5 km/h (viteza medie om)
- Distanță max: 500m (考虑comfort utilizator)
- Dijkstra poate permite și distanțe mai mari prin parametrul `maxWalkingDistance`

**Exemplu real**: 
```
Piața Mare → Piața Mică: 200m ≈ 2.4 minute walking
Dijkstra va prefera acest walking dacă autobuzul ar face un ocol lung
```

---

### Q6: Care este complexitatea algoritmului și cum o demonstrați?

**Răspuns:**

**Complexitate timp: O((V + E) log V)**

**Demonstrație:**

Algoritmul are două operații principale:

1. **Extragere din Priority Queue**: O(log V)
   - Se face pentru fiecare nod maximum o dată
   - Total: V × O(log V) = **O(V log V)**

2. **Relaxarea muchiilor**: O(log V) per muchie
   - Pentru fiecare muchie (e ∈ E), verificăm și eventual actualizăm distanța
   - Actualizarea în priority queue: O(log V)
   - Total: E × O(log V) = **O(E log V)**

**Combinat: O(V log V) + O(E log V) = O((V + E) log V)**

**Complexitate spațiu: O(V + E)**
- Stocarea grafului: O(V + E)
- Dicționare (distances, predecessors, visited): O(V)
- Priority Queue: O(V)
- **Total: O(V + E)**

**Pentru Sibiu:**
- V = 487 stații
- E ≈ 2,100 muchii (bus + transfer + walking)
- O((487 + 2100) × log 487) ≈ O(2600 × 9) ≈ **23,400 operații**

vs. Brute-force cu 2 transferuri: O(R³ × S²) ≈ O(45³ × 487²) ≈ **2 miliarde operații**

**Îmbunătățire: ~86,000× mai rapid!**

---

### Q7: Cum asigurați că găsiți drumul optim, nu doar unul valid?

**Răspuns:**

**Teorema de optimalitate a lui Dijkstra** (Demonstrație sketch):

**Invariant**: La fiecare pas, când un nod este marcat ca "vizitat", `distances[node]` conține **drumul cel mai scurt** de la start la acel nod.

**Proof by induction:**

**Bază**: La start, `distances[start] = 0`, care este optim (nu există drum mai scurt către sine).

**Pas inductiv**: Presupunem că invariantul este adevărat pentru toate nodurile vizitate până acum.

Când extragem următorul nod `u` cu costul minim din queue:
1. Orice alt drum către `u` ar trebui să treacă printr-un nod nevizitat `v`
2. Dar `distance[v] >= distance[u]` (altfel am fi extras mai întâi `v`)
3. Drumul prin `v` ar fi: `distance[v] + cost(v→u) >= distance[u]`
4. Deci nu poate exista drum mai scurt către `u`
5. **QED: invariantul rămâne adevărat**

**Condiție**: Funcționează doar pentru **greutăți non-negative**. 

În cazul meu:
- Timp călătorie > 0 ✓
- Transfer penalty > 0 ✓
- Walking time > 0 ✓

**Concluzie**: Djkstra garantează **matematic** drumul optim în aplicația mea.

---

## 💻 Întrebări Implementare

### Q8: Cum stocați graful în memorie?

**Răspuns:**

Folosesc o **listă de adiacență** (adjacency list):

```csharp
public class TransportGraph
{
    // Dictionary: StationId → GraphNode
    public Dictionary<int, GraphNode> Nodes { get; set; }
}

public class GraphNode
{
    public int StationId { get; set; }
    public Station Station { get; set; }
    
    // Lista de muchii către vecini
    public List<GraphEdge> Edges { get; set; }
}
```

**De ce lista de adiacență?**

| Reprezentare | Spațiu | Găsire vecini | Verificare muchie |
|-------------|--------|---------------|-------------------|
| Matrice adiacență | O(V²) | O(V) | O(1) |
| **Listă adiacență** | **O(V+E)** | **O(deg(v))** | **O(deg(v))** |

Pentru un graf **rar** (sparse) ca rețeaua de transport:
- V = 487, dar fiecare stație are doar ~4 vecini în medie
- Matrice: 487² = 237,169 celule (majoritatea 0) → 2MB RAM
- Listă: 487 + 2100 = 2587 celule → 100KB RAM

**Economie de 20× memorie!**

---

### Q9: Cum implementați Priority Queue?

**Răspuns:**

Folosesc `PriorityQueue<TElement, TPriority>` din .NET 6+:

```csharp
var queue = new PriorityQueue<int, double>();
// int = StationId
// double = priority (cost/distanță)

queue.Enqueue(stationId, cost);  // O(log n)
var next = queue.Dequeue();      // O(log n)
```

**Internals**: Implementat ca **binary min-heap**

```
        [1, cost:5]
       /           \
  [2, cost:8]    [3, cost:12]
  /         \
[4, cost:15] [5, cost:20]
```

**Operații:**
- `Enqueue`: Adaugă la final, apoi "bubble up" → O(log n)
- `Dequeue`: Extrage rădăcina, mută ultimul la început, "bubble down" → O(log n)

**Alternativa** (mai veche): Sortare la fiecare pas → O(n log n), mult mai lent!

---

### Q10: Cum reconstruiți path-ul final?

**Răspuns:**

Folosesc un dicționar de **predecesori** pentru a urmări drumul:

```csharp
// La fiecare relaxare de muchie:
if (newDistance < distances[neighbor])
{
    distances[neighbor] = newDistance;
    predecessors[neighbor] = (currentNode, edge);  // <-- salvez path-ul
    queue.Enqueue(neighbor, newDistance);
}
```

**Reconstrucția path-ului** merge backward de la destinație la start:

```csharp
private List<GraphNode> ReconstructPath(predecessors, start, end)
{
    var path = new List<GraphNode>();
    var current = end;
    
    // Merge înapoi
    while (current != start)
    {
        var (prevNode, edge) = predecessors[current];
        path.Add(graph.Nodes[current]);
        current = prevNode.StationId;
    }
    
    path.Add(graph.Nodes[start]);
    path.Reverse();  // Inversăm pentru a avea start → end
    
    return path;
}
```

**Exemplu:**
```
Predecessors:
  Hipodrom ← (Gară, Linia 5)
  Gară ← (Teatru, Linia 5)
  Teatru ← (Piața Unirii, Linia 5)

Reconstrucție:
  current = Hipodrom
    → prevNode = Gară, add Hipodrom
  current = Gară
    → prevNode = Teatru, add Gară
  current = Teatru
    → prevNode = Piața Unirii, add Teatru
  current = Piața Unirii
    → STOP (am ajuns la start), add Piața Unirii

Path: [Hipodrom, Gară, Teatru, Piața Unirii]
Reverse: [Piața Unirii, Teatru, Gară, Hipodrom] ✓
```

---

### Q11: Cum generați 3 rute alternative diferite?

**Răspuns:**

Rulez Dijkstra de **3 ori** cu parametri diferiți:

```csharp
// Ruta 1: Optimă (minimizare timp total)
var optimal = Dijkstra(graph, start, end, 
    transferPenalty: 5);

// Ruta 2: Favorează rute directe (penalitate mare transfer)
var direct = Dijkstra(graph, start, end, 
    transferPenalty: 15);  // 3× mai mare

// Ruta 3: Permite mai mult walking
var walking = Dijkstra(graph, start, end, 
    transferPenalty: 5, 
    maxWalkingDistance: 0.75);  // 750m în loc de 500m
```

**Filtrare duplicate:**
```csharp
if (!PathsAreEquivalent(optimal, direct))
    alternatives.Add(direct);
```

**Rezultat**: 2-3 rute distincte, oferind utilizatorului opțiuni:
- **Cea mai rapidă**: 15 min cu un transfer
- **Fără transfer**: 20 min direct
- **Cu walking**: 18 min (5 min bus + 3 min walking)

**Beneficiu UX**: Utilizatorul alege bazat pe preferințe (confort vs. viteză)

---

## 🔧 Întrebări Optimizare

### Q12: Cum optimizați construirea grafului?

**Răspuns:**

Graful se construiește **o singură dată** la pornirea backend-ului:

```csharp
// În Program.cs - la startup
builder.Services.AddSingleton<TransportGraph>(serviceProvider =>
{
    var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
    var routes = context.Routes.Include(r => r.RouteStations).ToList();
    var stations = context.Stations.ToList();
    
    return BuildTransportGraph(routes, stations);  // ~45ms
});

// Inject cached graph în service
builder.Services.AddScoped<RouteCalculatorService>();
```

**Beneficii:**
1. **Build doar o dată**: 45ms la startup vs. 45ms per request
2. **Shared între requests**: Toate query-urile folosesc același graf
3. **Memory efficient**: Un singur graf în RAM (~100KB)

**Rebuild**: Doar când se actualizează rute/stații (configurare admin)

---

### Q13: Există bottleneck-uri de performanță?

**Răspuns:**

**Profilare** (cu dotnet-trace):

```
Operație                    | Timp    | % Total
----------------------------|---------|--------
Build graph (startup)       | 45ms    | N/A
Dijkstra query             | 8-15ms  | 60%
  - Priority queue ops     | 6ms     | 40%
  - Edge relaxation        | 2ms     | 13%
Path reconstruction        | 2ms     | 13%
  - Segment building       | 1.5ms   | 10%
DB queries (route details) | 3ms     | 20%
JSON serialization         | 1ms     | 7%
----------------------------|---------|--------
TOTAL per request          | 15-20ms | 100%
```

**Observații:**
- ✅ Priority queue (6ms) e inevitabil pentru O(log V)
- ✅ Request total <20ms e excelent pentru UX
- ✅ Niciun bottleneck major

**Posibile optimизări** (dacă ar fi necesar):
1. **Bidirectional Dijkstra**: Căutare simultană start→end și end→start, reduce la ~8ms
2. **Precompute popular routes**: Cache pentru "Centru → Aeroport"
3. **Index geografic**: Reduce walking edges folosind R-tree

Dar **nu sunt necesare** pentru dimensiunea actuală a rețelei.

---

## 🚀 Întrebări Extensibilitate

### Q14: Cum ați extinde algoritmul pentru integrarea cu orare reale?

**Răspuns:**

**Time-dependent Dijkstra**:

```csharp
// În loc de cost fix:
var travelTime = edge.TravelTime;

// Cost variabil bazat pe ora curentă:
var travelTime = GetTravelTime(edge, currentTime);

private double GetTravelTime(GraphEdge edge, DateTime time)
{
    if (edge.Type == EdgeType.Bus)
    {
        // Găsește următoarea plecare pe această linie
        var schedule = GetSchedule(edge.RouteId, edge.FromStationId, time);
        
        if (schedule == null)
            return double.MaxValue;  // Nu mai circulă
        
        var waitTime = (schedule.DepartureTime - time).TotalMinutes;
        var busTime = edge.TravelTime;
        
        return waitTime + busTime;
    }
    
    return edge.TravelTime;  // Walking/Transfer - timp fix
}
```

**Modificări necesare:**
1. Adaugă tabel `Schedules` în DB
2. Modifică funcția de cost în Dijkstra
3. Dijkstra rămâne identic, doar costurile se schimbă!

**Beneficiu**: Rute realiste bazate pe ora plecării, nu estimări.

---

### Q15: Cum ar funcționa pentru o rețea mai mare (ex: București, Paris)?

**Răspuns:**

Pentru rețele mari (10,000+ stații), Dijkstra devine mai lent:

**Optimizări posibile:**

1. **Contraction Hierarchies (CH)**:
   - Preprocesare: Creează "shortcuts" pentru nodeuri importante
   - Query time: <1ms (vs 50ms Dijkstra standard)
   - Folosit de Google Maps

2. **Bidirectional Search**:
   - Căutare simultană start→end și end→start
   - Stop când se întâlnesc
   - Speedup: ~2× mai rapid

3. **A\* cu heuristică mai sofisticată**:
   - h(n) = distanță_linie_dreaptă / viteza_max
   - Admisibilă dacă viteza_max ≥ viteza_reală

4. **Partitionare geografică**:
   - Împarte orașul în zone
   - Inter-zone queries folosesc "gateway stations"

**Pentru Sibiu actual**: Dijkstra e **perfect adecvat**. Optimizările de mai sus ar fi "premature optimization".

---

## 🎯 Întrebări Finale

### Q16: Care ar fi următorul pas de îmbunătățire?

**Răspuns:**

**Prioritate înaltă:**
1. **Integrare orare reale** (time-dependent Dijkstra)
2. **Historical data** pentru costuri mai precise (ore de vârf)
3. **A/B testing** pentru validarea rutelor cu utilizatori reali

**Nice-to-have:**
1. **Multi-criteria optimization** (timp + confort + cost)
2. **Real-time incident handling** (drumuri blocate, autobuze anulate)
3. **Personalizare** (prefer walking vs prefer bus)

---

### Q17: Ce ați învățat implementând acest algoritm?

**Răspuns:**

**Aspect tehnic:**
- Diferența între cunoașterea teoretică (curs) și implementare reală
- Importanța structurilor de date (Priority Queue vs sortare naivă)
- Modelarea unei probleme reale ca graf

**Aspect practic:**
- Dijkstra e puternic dar trebuie adaptat (penalități, costuri domain-specific)
- Testarea e crucială (am găsit bug-uri în edge cases)
- Documentația ajută enorm la debugging

**Aspect profesional:**
- Cum să argumentez alegerea unui algoritm
- Trade-off-uri între simplitate (brute-force) și optimizare (Dijkstra)
- Cum să explic concepte tehnice non-tehnicienilor

---

## ✅ Sfaturi pentru Prezentare

1. **Pregătește o demo live**: Arată un request API și explică step-by-step
2. **Desenează pe tablă**: Exemplu simplu (4-5 noduri) executat manual
3. **Menționează complexitatea**: Comisia apreciază analiza teoretică
4. **Recunoaște limitele**: "Pentru o rețea globală, aș folosi Contraction Hierarchies"
5. **Leagă de industrie**: "Uber folosește variante ale acestui algoritm"
6. **Fii pregătit pentru code review**: Poți explica orice linie din implementare

**Fii confident!** Implementarea ta e la nivel profesional 🚀
