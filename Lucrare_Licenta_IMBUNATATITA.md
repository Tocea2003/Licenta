# STRUCTURA ÎMBUNĂTĂȚITĂ A LUCRĂRII DE LICENȚĂ

## Ghid pentru poze, diagrame și îmbunătățiri

---

## STRUCTURA NOUĂ PROPUSĂ (vs. cea veche)

### Cuprins nou:

1. **Introducere** (păstrat, ușor extins)
2. **Capitolul 1. Context și fundamente** (păstrat, ușor extins)
3. **Capitolul 2. Considerații teoretice** ← **NOU (capitol complet lipsă!)**
4. **Capitolul 3. Analiza și proiectarea aplicației** (restructurat din fostul Cap. 2)
5. **Capitolul 4. Implementarea aplicației** (restructurat din fostul Cap. 3)
6. **Capitolul 5. Descrierea aplicației și interfața grafică** (restructurat din fostul Cap. 4)
7. **Capitolul 6. Testarea aplicației** ← **Separat ca capitol independent**
8. **Concluzii**
9. **Bibliografie**

---

## DETALII PER CAPITOL - CE TREBUIE ADĂUGAT

---

### INTRODUCERE (pagina 7-10)
**Status:** Bună, necesită mici ajustări.

**De adăugat:**
- Un paragraf care descrie structura lucrării pe capitole (ce conține fiecare capitol)
- Exemplu: „Lucrarea este structurată în 6 capitole. Capitolul 1 prezintă contextul... Capitolul 2 fundamentează teoretic... etc."

---

### CAPITOLUL 1. Context și fundamente (paginile 11-16)
**Status:** Bun, dar necesită completări.

**De adăugat:**

📊 **DIAGRAMA 1.1** — Diagrama de tip radar/spider chart care compară vizual cele 4 platforme (Google Maps, Moovit, Tranzy, Soluția propusă) pe criteriile din tabel.
> Locație: După Tabelul 1.1 (comparația platformelor)
> Tip: Diagramă radar cu 7 axe (Urmărire timp real, Calcul rute, Grad ocupare, Mod offline, Panou admin, Open-source, Personalizare)

🖼️ **POZĂ 1.1** — Screenshot din Google Maps Transit (modul de transport public)
> Locație: La secțiunea 1.3.1, după descrierea Google Maps
> Scop: Arată interfața competitor-ului

🖼️ **POZĂ 1.2** — Screenshot din aplicația Moovit
> Locație: La secțiunea 1.3.2
> Scop: Arată interfața competitor-ului

🖼️ **POZĂ 1.3** — Screenshot din aplicația Tranzy
> Locație: La secțiunea 1.3.3
> Scop: Arată interfața competitor-ului

---

### CAPITOLUL 2. Considerații teoretice ← **CAPITOL COMPLET NOU** (paginile 17-35)

Acesta este capitolul cel mai important care lipsește! Modelul Cepoiu are ~20 de pagini de fundamente teoretice. Trebuie scris de la zero:

#### 2.1. Arhitectura client-server și API-uri RESTful
- Definiția arhitecturii client-server
- Principiile REST (Representational State Transfer)
- Metodele HTTP (GET, POST, PUT, DELETE)
- Coduri de status HTTP
- Formatul JSON pentru schimbul de date

📊 **DIAGRAMA 2.1** — Diagrama fluxului cerere-răspuns HTTP într-o arhitectură REST
> Tip: Diagramă de secvență (Client → Server → Bază de date → Server → Client)

#### 2.2. C# și platforma .NET
- Prezentare generală a limbajului C#
- Tipuri valoare vs. tipuri referință
- Programarea orientată pe obiecte (Încapsulare, Moștenire, Polimorfism, Abstracție)
- Garbage Collector și managementul memoriei
- LINQ (Language Integrated Query)

#### 2.3. ASP.NET Core
- Prezentare generală a framework-ului
- Arhitectura middleware și pipeline-ul HTTP
- Dependency Injection (Singleton, Scoped, Transient)
- Entity Framework Core ca ORM

📊 **DIAGRAMA 2.2** — Pipeline-ul de middleware ASP.NET Core
> Tip: Diagramă de flux (Request → Middleware 1 → Middleware 2 → ... → Controller → Response)
> Referință: Similar cu Figura 2.1 din modelul Cepoiu

#### 2.4. Autentificarea JWT Bearer
- Structura unui JSON Web Token (Header, Payload, Signature)
- Schema Bearer și fluxul de autentificare
- Avantajele autentificării stateless

📊 **DIAGRAMA 2.3** — Schema de autentificare JWT Bearer
> Tip: Diagramă de secvență (Client → Login → Server generează JWT → Client trimite JWT la fiecare request)

#### 2.5. SQLite și bazele de date relaționale
- Modelul relațional (tabele, chei primare, chei externe)
- Normalizarea bazelor de date (1FN, 2FN, 3FN)
- Proprietățile ACID
- SQLite vs. alte SGBD-uri (de ce e potrivit pentru proiect)

#### 2.6. Firebase Realtime Database
- Modelul de date NoSQL (JSON tree)
- Sincronizarea în timp real prin WebSockets
- Comparație Firebase Realtime Database vs. Firestore
- Reguli de securitate Firebase

📊 **DIAGRAMA 2.4** — Comparație model relațional (SQLite) vs. model NoSQL (Firebase)
> Tip: Diagramă side-by-side cu tabele relaționale vs. arbore JSON

#### 2.7. Vue.js 3 și ecosistemul frontend
- Modelul bazat pe componente
- Composition API vs. Options API
- Reactivitatea în Vue 3 (ref, reactive, computed)
- Virtual DOM și randarea eficientă
- Vue Router pentru navigarea SPA
- Pinia pentru managementul stării

📊 **DIAGRAMA 2.5** — Ciclul de viață al unei componente Vue 3
> Tip: Flowchart (setup → onBeforeMount → onMounted → onBeforeUpdate → onUpdated → onBeforeUnmount → onUnmounted)

#### 2.8. TypeScript
- Tipizarea statică vs. dinamică
- Interfețe și tipuri
- Generice
- Avantajele față de JavaScript pur

#### 2.9. Leaflet și hărțile interactive
- Arhitectura tile-based
- Layere și markere
- Marker clustering
- Integrarea cu servicii de geocoding (Nominatim OSM)

#### 2.10. Progressive Web Apps (PWA)
- Definiția și caracteristicile PWA
- Service Workers și strategii de caching
- Web App Manifest
- IndexedDB pentru stocare offline

📊 **DIAGRAMA 2.6** — Strategiile de caching ale Service Worker-ului
> Tip: Diagramă de flux cu cele 3 strategii: Cache-First, Network-First, Network-Only

#### 2.11. Algoritmul Dijkstra
- Prezentare generală și complexitate
- Pseudocodul algoritmului
- Cozi de priorități (min-heap)
- Formula Haversine pentru distanțe GPS
- Aplicații în sisteme de transport

📊 **DIAGRAMA 2.7** — Exemplu pas-cu-pas al algoritmului Dijkstra pe un graf mic
> Tip: Serie de 4-5 imagini care arată evoluția algoritmului (noduri vizitate, distanțe actualizate)

#### 2.12. Formatul GTFS (General Transit Feed Specification)
- Structura fișierelor GTFS (routes.txt, stops.txt, trips.txt, etc.)
- Relațiile între fișierele GTFS
- Adoptarea la nivel mondial

📊 **DIAGRAMA 2.8** — Diagrama relațiilor între fișierele GTFS
> Tip: Diagramă ER simplificată (routes → trips → stop_times → stops, trips → shapes)

---

### CAPITOLUL 3. Analiza și proiectarea aplicației (paginile 36-55)
**Status:** Restructurat din fostul Capitolul 2.

**De adăugat:**

#### 3.1. Descrierea cerințelor funcționale și non-funcționale
(Restructurat din 2.1, dar cu o separare mai clară)

**Tabel nou:**
📊 **TABELUL 3.1** — Cerințe funcționale detaliate
| ID | Cerință | Prioritate | Actor |
|----|---------|-----------|-------|
| CF1 | Vizualizare autobuze pe hartă în timp real | Ridicată | Vizitator |
| CF2 | Calcul rute optime cu transfer | Ridicată | Utilizator |
| ... | ... | ... | ... |

📊 **TABELUL 3.2** — Cerințe non-funcționale
| ID | Cerință | Metrică |
|----|---------|---------|
| CNF1 | Timpul de răspuns API < 200ms | Măsurat cu Chrome DevTools |
| CNF2 | Suport offline complet | Testat cu deconectarea rețelei |
| ... | ... | ... |

#### 3.1.1. Actorii sistemului ← **NOU**

📊 **DIAGRAMA 3.1** — Diagrama actorilor sistemului
> Tip: Diagramă UML cu 3 actori: Vizitator, Utilizator autentificat, Administrator
> Notă: Arată ierarhia și moștenirea permisiunilor

#### 3.1.2. Cazuri de utilizare (Use Cases) ← **NOU**

📊 **DIAGRAMA 3.2** — Diagrama Use Case pentru Vizitator/Utilizator neautentificat
> Conține: Vizualizare hartă, Căutare stații, Înregistrare, Autentificare

📊 **DIAGRAMA 3.3** — Diagrama Use Case pentru Utilizator autentificat
> Conține: Toate cele de la Vizitator + Planificare călătorie, Salvare favorite, Setare notificări, Vizualizare istoric

📊 **DIAGRAMA 3.4** — Diagrama Use Case pentru Administrator
> Conține: Toate cele de la Utilizator + Gestionare rute, Gestionare stații, Dashboard analitic, Import GTFS, Monitorizare flotă

#### 3.2. Arhitectura aplicației (restructurat din 2.2)

🖼️ **POZĂ 3.1** — Diagrama arhitecturală a aplicației (deja existentă ca Figura 2.1)
> Păstrează imaginea existentă, dar mărește-o și adaugă legende mai clare

📊 **DIAGRAMA 3.5** — Diagrama de deployment
> Tip: Diagramă UML de deployment care arată: Frontend (Vue 3 pe port 5173) → Backend (ASP.NET Core pe port 5022) → SQLite DB + Firebase + BusSimulator
> Arată și protocoalele: HTTP/REST, WebSocket, Firebase SDK

📊 **DIAGRAMA 3.6** — Diagrama de componente
> Tip: Diagramă UML care arată modulele: AuthModule, RoutingModule, GTFSModule, AdminModule, MapModule, NotificationModule, OfflineModule

#### 3.3. Structura bazei de date (restructurat din 2.3)

🖼️ **POZĂ 3.2** — Diagrama ER completă (deja existentă ca Figura 2.2)
> Păstrează, dar adaugă și tabelele GTFS (Trip, StopTime, Shape)

📊 **DIAGRAMA 3.7** — Diagrama bazei de date Firebase
> Tip: Arborescent JSON
```
/buses
  /{busId}
    /latitude: number
    /longitude: number  
    /occupancy: number (0-100)
    /routeId: number
    /heading: number
    /speed: number
```

#### 3.4. Algoritmul Dijkstra adaptat (restructurat din 2.4)

📊 **DIAGRAMA 3.8** — Flowchart-ul algoritmului de calcul al rutelor
> Tip: Diagramă de activitate UML
> Pași: Input coordonate → Găsire stații apropiate → Căutare rute directe → Dacă nu există → Căutare cu transfer → Generare 3 alternative → Output

📊 **DIAGRAMA 3.9** — Exemplu concret de graf multimodal (deja existentă ca Figura 2.3)
> Îmbunătățire: Adaugă numere pe muchii (costurile în minute) și colorează rutele diferit

📊 **DIAGRAMA 3.10** — Pseudocodul algoritmului Dijkstra adaptat
> Tip: Bloc de pseudocod formatat frumos (nu cod sursă, ci pseudocod academic)
```
FUNCȚIE CalculeazăRutăOptimă(start, destinație, penalizareTransfer)
  graf ← ConstruieșteGraf(stații, rute)
  distanțe ← {∞ pentru toate stările}
  distanțe[start] ← 0
  Q ← CoadăPrioritate cu (0, start)
  
  CÂT TIMP Q nu este goală:
    (cost, stare) ← ExtrageMinim(Q)
    DACĂ stare == destinație: RETURNEAZĂ ReconstruieșteTraseul()
    PENTRU FIECARE vecin al stării:
      costNou ← cost + CostMuchie(stare, vecin)
      DACĂ vecin.rută ≠ stare.rută: costNou += penalizareTransfer
      DACĂ costNou < distanțe[vecin]:
        distanțe[vecin] ← costNou
        Adaugă (costNou, vecin) în Q
  
  RETURNEAZĂ "Nu există rută"
```

---

### CAPITOLUL 4. Implementarea aplicației (paginile 56-80)
**Status:** Restructurat din fostul Capitolul 3. Trebuie adăugate fragmente de cod și mai multe diagrame.

#### 4.1. Crearea și configurarea proiectului

📊 **DIAGRAMA 4.1** — Structura de directoare a proiectului
> Tip: Arbore de fișiere (tree)
```
TursibTracker/
├── TursibBackend/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── RoutingController.cs
│   │   ├── RoutesController.cs
│   │   ├── StationsController.cs
│   │   └── ...
│   ├── Models/
│   ├── Services/
│   ├── Migrations/
│   └── Program.cs
├── TursibBackend.Tests/
├── BusSimulator/
└── tursib-frontend/
    ├── src/
    │   ├── views/ (14 view-uri)
    │   ├── components/ (30+ componente)
    │   ├── composables/ (10 module)
    │   └── router/
    └── public/
```

#### 4.2. Implementarea autentificării ← **Secțiune restructurată**

##### 4.2.1. Modelul de date pentru autentificare
(Fragment de cod cu clasa User)

📊 **DIAGRAMA 4.2** — Flowchart-ul procesului de autentificare
> Tip: Diagramă de activitate (similar cu Figura 3.7 din modelul Cepoiu)
> Pași: Primire cerere → Validare DTO → Căutare user → Verificare BCrypt → Generare JWT → Răspuns

**Fragment de cod de adăugat:**
```csharp
// Exemplu din AuthController - endpoint de login
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Username == loginDto.Username);
    
    if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        return Unauthorized("Invalid credentials");
    
    var token = _jwtService.GenerateToken(user);
    return Ok(new { token, user.Username, user.Role });
}
```

##### 4.2.2. Generarea token-urilor JWT
(Fragment de cod cu JwtService.GenerateToken)

📊 **DIAGRAMA 4.3** — Diagrama de secvență a autentificării Google OAuth 2.0
> Tip: Diagramă de secvență UML
> Participanți: Client (Vue 3) → Google Sign-In → Backend (ASP.NET Core) → Baza de date
> Pași: 1. Click "Sign in with Google" → 2. Google returnează credential → 3. Frontend trimite credential la backend → 4. Backend decodează JWT Google → 5. Creează/găsește user → 6. Generează JWT propriu → 7. Returnează JWT

#### 4.3. Importul datelor GTFS

📊 **DIAGRAMA 4.4** — Diagrama de secvență a importului GTFS
> Tip: Diagramă de secvență
> Pași secvențiali: Parse routes.txt → Parse stops.txt → Parse shapes.txt → Parse trips.txt → Parse stop_times.txt → Construiește relații rută-stație

🖼️ **POZĂ 4.1** — Fluxul de import GTFS (deja existentă ca Figura 3.1)

**Fragment de cod de adăugat:**
```csharp
// Exemplu din GTFSImporter - selecția cursei reprezentative
var tripGroups = trips.GroupBy(t => t.RouteId);
foreach (var group in tripGroups)
{
    var stopCounts = group.Select(t => t.StopTimes.Count);
    var modeCount = stopCounts.GroupBy(c => c)
        .OrderByDescending(g => g.Count())
        .First().Key;
    
    var representativeTrip = group
        .Where(t => t.StopTimes.Count == modeCount)
        .OrderBy(t => t.StopTimes.Min(st => st.DepartureTime))
        .First();
    // ... construiește relațiile RouteStation
}
```

#### 4.4. Motorul de calcul al rutelor (RouteCalculatorService)

**Fragment de cod de adăugat:**
```csharp
// Codificarea stărilor în graf
private long EncodeState(int stationId, int routeId)
    => (long)stationId * 10_000_000 + routeId;

// Formula Haversine
private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371; // Raza Pământului în km
    var dLat = ToRadians(lat2 - lat1);
    var dLon = ToRadians(lon2 - lon1);
    var a = Math.Sin(dLat/2) * Math.Sin(dLat/2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon/2) * Math.Sin(dLon/2);
    return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
}
```

📊 **DIAGRAMA 4.5** — Exemplu concret de calcul al rutei
> Tip: Diagramă cu harta Sibiului simplificată, arătând:
> - Punctul de start (coordonate GPS)
> - Stațiile cele mai apropiate (raza de 500m)
> - Ruta calculată cu Dijkstra (evidențiată pe hartă)
> - Punctul de destinație

#### 4.5. Componentele frontend-ului

📊 **DIAGRAMA 4.6** — Arborele de componente Vue 3
> Tip: Diagramă arborescentă
```
App.vue
├── TopNavbar
├── RouterView
│   ├── HomeView
│   │   ├── MapView (Leaflet)
│   │   ├── EnhancedSearch
│   │   ├── NearbyStationsPanel
│   │   └── AdvancedTripPlanner
│   │       └── AlternativeRoutesPanel
│   ├── FavoritesView
│   ├── AdminDashboard
│   │   ├── AdminAnalytics (Chart.js)
│   │   ├── AdminRoutes
│   │   └── AdminStations
│   ├── LoginView
│   └── SettingsView
└── BottomNav (mobile)
```

**Fragment de cod de adăugat:**
```typescript
// Exemplu din composable-ul useNotifications
const checkBusProximity = (bus: BusPosition, station: Station) => {
  const distance = haversineDistance(
    bus.latitude, bus.longitude,
    station.latitude, station.longitude
  );
  const eta = (distance / 25) * 60; // minute, la 25 km/h
  
  if (eta <= notificationThreshold.value) {
    sendNotification({
      title: `Autobuzul ${bus.routeNumber} se apropie!`,
      body: `ETA: ${Math.round(eta)} minute`,
      tag: `bus-${bus.id}-station-${station.id}`
    });
  }
};
```

#### 4.6. Simulatorul de autobuze (BusSimulator)

📊 **DIAGRAMA 4.7** — Diagrama de activitate a simulatorului
> Tip: Flowchart
> Pași: Încarcă rute din API → Pentru fiecare autobuz (parallel) → Încarcă stații rută → Obține puncte traseu (GTFS Shapes / OSRM / Direct) → Bucla: Deplasează pe traseu → Actualizează ocupare → Scrie în Firebase → Așteaptă 2-5s → Repetă

📊 **DIAGRAMA 4.8** — Mecanism de fallback pe 3 niveluri
> Tip: Diagramă de decizie
```
Încearcă GTFS Shapes
  ├── Succes → Folosește punctele GTFS (100.000+ puncte GPS)
  └── Eșec → Încearcă OSRM Routing
                ├── Succes → Folosește traseu rutier real
                └── Eșec → Folosește coordonate stații direct
```

#### 4.7. Progressive Web App (PWA) ← **Secțiune nouă separată**

📊 **DIAGRAMA 4.9** — Arhitectura Service Worker cu cele 3 strategii de caching
> Tip: Diagramă de flux
```
Request vine de la aplicație
  │
  ├── Este resursă statică? (HTML, CSS, JS, iconuri)
  │     └── DA → Strategia Cache-First
  │           ├── Există în cache? → DA → Returnează din cache
  │           └── NU → Descarcă de pe rețea → Salvează în cache → Returnează
  │
  ├── Este cerere API? (/api/*)
  │     └── DA → Strategia Network-First
  │           ├── Rețea disponibilă? → DA → Descarcă → Salvează în cache → Returnează
  │           └── NU → Returnează din cache (dacă există)
  │
  └── Este cerere Firebase?
        └── DA → Strategia Network-Only
              └── Fără caching (date real-time)
```

#### 4.8. Optimizări de performanță

📊 **DIAGRAMA 4.10** — Grafic bar chart cu rezultatele optimizărilor
> Tip: Grafic cu bare duble (Înainte vs. După) pentru cele 4 metrici
> Date: Timp încărcare (3.5s → 1.8s), Bundle (2.5MB → 1.5MB), Memorie (180MB → 120MB), CPU idle (45% → 15%)

---

### CAPITOLUL 5. Descrierea aplicației și interfața grafică (paginile 81-95)
**Status:** Restructurat din fostul Capitolul 4. Necesită toate screenshot-urile reale.

#### 5.1. Navigarea și interacțiunea în aplicație

📊 **DIAGRAMA 5.1** — Diagrama de navigare a aplicației (Sitemap)
> Tip: Diagramă arborescentă cu paginile și legăturile între ele
```
Pagina principală (Hartă)
├── Căutare stații → Detalii stație
├── Planificare călătorie → Rezultate rute
├── Favorite
├── Setări
│   ├── Mod întunecat
│   ├── Notificări
│   └── Limbă
├── Login / Register
└── Admin Panel (doar Admin)
    ├── Dashboard
    ├── Gestionare Rute
    ├── Gestionare Stații
    └── Monitorizare Flotă
```

#### 5.2. Interfața utilizatorului obișnuit

🖼️ **POZĂ 5.1** — Harta principală cu autobuzele în timp real (OBLIGATORIU)
> Capturează: Harta Leaflet cu markere colorate (verde/galben/roșu) + markere stații cluster-uite
> Notă: Arată și tooltip-ul unui autobuz (linia, ETA, grad ocupare)

🖼️ **POZĂ 5.2** — Bara de căutare cu sugestii în timp real (OBLIGATORIU)
> Capturează: Câmpul de căutare cu dropdown-ul de sugestii deschis

🖼️ **POZĂ 5.3** — Planificarea călătoriilor - cele 3 rute alternative (OBLIGATORIU)
> Capturează: Panoul cu cele 3 opțiuni de rută (cea mai rapidă, mai puține transferuri, alternativă)
> Important: Arată și vizualizarea pe hartă a rutei selectate

🖼️ **POZĂ 5.4** — Detalii stație (OBLIGATORIU)
> Capturează: Pagina StationDetailsView cu lista rutelor care trec prin stație

🖼️ **POZĂ 5.5** — Pagina Favorite (OBLIGATORIU)
> Capturează: FavoritesView cu locații salvate (Casă, Serviciu, personalizate)

🖼️ **POZĂ 5.6** — Pagina de Login cu Google OAuth 2.0 (OBLIGATORIU)
> Capturează: Formularul de login cu butonul "Sign in with Google"

🖼️ **POZĂ 5.7** — Pagina de Înregistrare (OBLIGATORIU)
> Capturează: Formularul de Sign Up

🖼️ **POZĂ 5.8** — Notificare browser la apropierea autobuzului (OBLIGATORIU)
> Capturează: Notificarea push/browser cu mesajul "Autobuzul Linia X se apropie!"

🖼️ **POZĂ 5.9** — Setări cu Dark Mode activat (OBLIGATORIU)
> Capturează: Aplicația în modul întunecat

#### 5.3. Interfața administratorului

🖼️ **POZĂ 5.10** — Dashboard-ul administrativ cu grafice Chart.js (OBLIGATORIU)
> Capturează: Pagina AdminDashboard cu cele 4 grafice vizibile

🖼️ **POZĂ 5.11** — Pagina de gestionare a rutelor (OBLIGATORIU)
> Capturează: AdminRoutes cu lista rutelor și formular de editare

🖼️ **POZĂ 5.12** — Pagina de gestionare a stațiilor cu hartă (OBLIGATORIU)
> Capturează: AdminStations cu harta și markere-le stațiilor editabile

🖼️ **POZĂ 5.13** — Monitorizarea în timp real a flotei (OBLIGATORIU)
> Capturează: Lista autobuzelor active cu poziție, viteză, grad ocupare

#### 5.4. Interfața mobilă

🖼️ **POZĂ 5.14** — Interfața mobilă cu Bottom Navigation (OBLIGATORIU)
> Capturează: Screenshot de pe telefon/emulator cu cele 4 tab-uri vizibile

🖼️ **POZĂ 5.15** — Interfața mobilă - Planificare călătorii (OBLIGATORIU)
> Capturează: Aceeași funcționalitate pe ecran mic

🖼️ **POZĂ 5.16** — Dialogul de instalare PWA (OPȚIONAL dar recomandat)
> Capturează: Banner-ul "Adaugă pe ecranul de start"

---

### CAPITOLUL 6. Testarea aplicației ← **Capitol separat** (paginile 96-105)
**Status:** Extras din fostul 4.3 și extins semnificativ.

**De adăugat:**

📊 **DIAGRAMA 6.1** — Piramida de testare a aplicației
> Tip: Piramidă cu 3 niveluri
```
        /  Teste E2E  \        (manuale)
       /  (acceptanță)  \
      /__________________ \
     /  Teste Integrare    \   (API endpoints)
    /______________________ \
   / Teste Unitare (71 teste) \ (xUnit, Vitest)
  /____________________________ \
```

📊 **TABELUL 6.1** — Acoperirea testelor pe module
| Modul | Nr. teste | Tip | Framework |
|-------|----------|-----|-----------|
| RouteCalculatorService | 31 | Unitare | xUnit |
| JwtService | 20 | Unitare | xUnit |
| useFavorites | 12 | Unitare | Vitest |
| useRecentSearches | 8 | Unitare | Vitest |
| BottomNav | N/A | Component | Vue Test Utils |

**Fragment de cod de adăugat:**
```csharp
// Exemplu test - verificare optimalitate Dijkstra
[Fact]
public void CalculateRoute_DirectRoute_ReturnsOptimalPath()
{
    // Arrange
    var service = new RouteCalculatorService(_context);
    var startLat = 45.7965; // Stația Piața Mare
    var startLng = 24.1519;
    var endLat = 45.7833;   // Stația Gara
    var endLng = 24.1455;
    
    // Act
    var result = service.CalculateRoute(startLat, startLng, endLat, endLng);
    
    // Assert
    Assert.NotNull(result);
    Assert.True(result.TotalDuration < TimeSpan.FromMinutes(30));
    Assert.True(result.Segments.Any(s => s.Type == "bus"));
}
```

🖼️ **POZĂ 6.1** — Screenshot cu rezultatele testelor xUnit (toate 51 trecute)
> Capturează: Output-ul din terminal cu toate testele verzi

🖼️ **POZĂ 6.2** — Screenshot cu rezultatele testelor Vitest (frontend)
> Capturează: Output-ul Vitest cu cele 20+ teste trecute

🖼️ **POZĂ 6.3** — Screenshot Lighthouse cu scorul de performanță
> Capturează: Raportul Lighthouse cu scorurile (Performance, Accessibility, Best Practices, SEO)

---

### CONCLUZII (paginile 106-108)
**Status:** Bune, necesită completare cu un paragraf despre contribuțiile academice.

**De adăugat:**
- Un paragraf care evidențiază contribuția academică/științifică a lucrării
- Menționarea potențialului de publicare ca articol
- Legătura cu tendințele actuale din Smart City

---

## REZUMAT TOTAL POZE ȘI DIAGRAME NECESARE

### Diagrame de creat (22 total):
1. Diagrama radar comparație platforme (Cap. 1)
2. Flux cerere-răspuns REST (Cap. 2)
3. Pipeline middleware ASP.NET Core (Cap. 2)
4. Schema autentificare JWT (Cap. 2)
5. Comparație SQL vs. NoSQL (Cap. 2)
6. Ciclu viață componentă Vue 3 (Cap. 2)
7. Strategii caching Service Worker (Cap. 2)
8. Algoritmul Dijkstra pas-cu-pas (Cap. 2)
9. Relații fișiere GTFS (Cap. 2)
10. Diagrama actorilor sistemului (Cap. 3)
11. Use Case Vizitator (Cap. 3)
12. Use Case Utilizator autentificat (Cap. 3)
13. Use Case Administrator (Cap. 3)
14. Diagrama de deployment (Cap. 3)
15. Diagrama de componente (Cap. 3)
16. Structura Firebase JSON (Cap. 3)
17. Flowchart algoritm rutare (Cap. 3)
18. Flowchart autentificare (Cap. 4)
19. Secvență Google OAuth (Cap. 4)
20. Arbore componente Vue (Cap. 4)
21. Diagrama activitate simulator (Cap. 4)
22. Piramida de testare (Cap. 6)

### Poze/Screenshot-uri necesare (19 total):
1-3. Screenshot-uri competitori (Google Maps, Moovit, Tranzy) - Cap. 1
4. Harta principală cu autobuze - Cap. 5
5. Bara de căutare cu sugestii - Cap. 5
6. Planificarea călătoriilor (3 rute) - Cap. 5
7. Detalii stație - Cap. 5
8. Pagina Favorite - Cap. 5
9. Pagina Login - Cap. 5
10. Pagina Register - Cap. 5
11. Notificare browser - Cap. 5
12. Dark Mode - Cap. 5
13. Dashboard admin cu grafice - Cap. 5
14. Gestionare rute - Cap. 5
15. Gestionare stații - Cap. 5
16. Monitorizare flotă - Cap. 5
17. Interfața mobilă - Cap. 5
18. Rezultate teste xUnit - Cap. 6
19. Raport Lighthouse - Cap. 6

---

## SFATURI PENTRU DIAGRAME

**Instrumente recomandate pentru creare diagrame:**
- **draw.io** (diagrams.net) — gratuit, online, exportă PNG/SVG
- **PlantUML** — pentru diagrame UML din cod text
- **Mermaid** — pentru diagrame în Markdown
- **Lucidchart** — alternativă premium
- **Visual Paradigm Community Edition** — gratuit pentru studenți

**Stil recomandat:**
- Folosește culori consistente (albastru pentru componente, verde pentru succes, roșu pentru erori)
- Dimensiune minimă 800x600 px pentru claritate la printare
- Adaugă legendă la fiecare diagramă complexă
- Font minim 10pt în diagrame
