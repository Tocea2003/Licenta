# ADR-002: SignalR pentru harta live, Firebase ca rezervă

- **Stare:** Acceptat
- **Data:** 2026-11-04

## Context
Pozițiile validate ajung în backend prin Kafka (ADR-001), deci harta trebuie să le primească
de la backend. În producție însă (Render free + Vercel) nu rulează Kafka, iar harta trebuie să
funcționeze în continuare.

## Decizie
- Backend-ul expune hub-ul **SignalR** `/hubs/vehicles`. Hub-ul există doar dacă
  `Kafka:BootstrapServers` e setat. Mesajele trimise:
  - `snapshot`: toate vehiculele, la conectare;
  - `positions`: doar vehiculele schimbate, în loturi la 500 ms;
  - `removed`: vehiculele care nu mai raportează.
- Frontend-ul are o singură sursă de date live, store-ul `liveBuses`:
  - pornește **imediat pe Firebase**, ca harta să nu stea goală;
  - încearcă în paralel hub-ul și **trece pe SignalR** la primele date primite;
  - revine pe Firebase dacă hub-ul se închide definitiv.
  - Pe Render, hub-ul răspunde 404, deci aplicația rămâne pe Firebase fără întârziere.
- Clientul SignalR se conectează fără credențiale, așa că politica CORS existentă (origini
  exacte) se aplică neschimbată.

## Consecințe
- (+) Componentele (MapView, AdminBuses, AdminAnalytics) nu știu de unde vin datele.
- (+) Lotul de 500 ms limitează traficul la ~2 mesaje/s per client, indiferent de flotă.
- (+) Biblioteca SignalR se încarcă leneș, într-un chunk separat (~56 KB), deci nu
  îngreunează prima încărcare.
- (−) Două surse de adevăr cât timp producția nu rulează pipeline-ul. Firebase rămâne
  alimentat doar dacă simulatorul are `PUBLISH_TARGETS` cu `firebase`.
