# ADR-002: SignalR pentru harta live, Firebase ca rezervă

- **Stare:** Acceptat
- **Data:** 2026-11-17

## Context
Pozițiile validate ajung în backend (ADR-001), deci harta trebuie să le primească de la backend. Implementările
existente (planul gratuit Render) pot adormi, iar harta nu trebuie să rămână goală.

## Decizie
- Backend-ul expune hub-ul **SignalR** `/hubs/buses`: `GetSnapshot()` la conectare, apoi evenimentul `positions` cu
  fiecare lot acceptat, plus evenimentul `bunching` cu alertele active.
- Frontend-ul are o singură sursă de date live, store-ul `liveBuses` (ref-counted, o singură conexiune pentru toată
  aplicația). Folosește SignalR și pornește **Firebase ca rezervă** doar cât timp SignalR nu livrează nimic de 35 s.
  Pragul e peste intervalul de raportare al dispozitivelor din cloud (10 s). `VITE_LIVE_SOURCE` forțează o sursă.
- Clientul SignalR se conectează fără credențiale, deci politica CORS cu origini exacte rămâne neschimbată.

## Consecințe
- (+) Componentele (MapView, AdminBuses, AdminAnalytics) nu știu de unde vin datele.
- (+) Un backend căzut sau adormit nu golește harta, cât timp simulatorul mai scrie în Firebase.
- (−) Două surse de adevăr cât timp Firebase e păstrat ca rezervă; se poate opri (`FIREBASE_ENABLED=false`).
