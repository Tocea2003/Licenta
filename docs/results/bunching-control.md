# Rezultate: controlul bunching-ului în digital twin

Generat cu `dotnet run -- twin-experiment` (din `TursibBackend/`). Politica RL a fost antrenată pe alte seed-uri și scenarii (vezi RlTrainer) și aleasă pe un set de validare separat.
Linie de 5.0 km pe sens, 4 h simulate, 20 replicări pe scenariu (seed-uri de test 1–20).
Valorile sunt medie ± abatere standard între replicări. Întârziere = așteptare în stație + timp pierdut în autobuz din holding, pe pasager.

## 3 autobuze, doar zgomot

| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |
|---|---|---|---|---|---|---|
| none | 399 ± 56 | 399 ± 56 | 99 ± 56 | 0.55 ± 0.17 | 0 ± 0 | 32.9 ± 15.9 |
| headway | 319 ± 5 | 312 ± 2 | 3 ± 1 | 0.10 ± 0.02 | 46 ± 16 | 0.0 ± 0.0 |
| equalize | 322 ± 6 | 313 ± 3 | 4 ± 1 | 0.11 ± 0.02 | 53 ± 20 | 0.0 ± 0.0 |
| rl | 313 ± 3 | 309 ± 1 | 1 ± 0 | 0.07 ± 0.01 | 27 ± 12 | 0.0 ± 0.0 |

- none − headway: +79.6 s/pasager (IÎ 95%: +54.8 … +104.4); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- equalize − headway: +2.3 s/pasager (IÎ 95%: +0.4 … +4.1); mai bun pe 6, mai slab pe 13 din 20 seed-uri
- rl − headway: -6.3 s/pasager (IÎ 95%: -8.8 … -3.9); mai bun pe 17, mai slab pe 3 din 20 seed-uri

## 3 autobuze, un autobuz oprit 5 min

| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |
|---|---|---|---|---|---|---|
| none | 737 ± 58 | 737 ± 58 | 407 ± 53 | 1.11 ± 0.07 | 0 ± 0 | 86.3 ± 1.1 |
| headway | 343 ± 4 | 325 ± 2 | 7 ± 1 | 0.15 ± 0.01 | 116 ± 14 | 0.0 ± 0.0 |
| equalize | 344 ± 3 | 326 ± 1 | 8 ± 0 | 0.16 ± 0.00 | 119 ± 11 | 0.0 ± 0.0 |
| rl | 336 ± 4 | 321 ± 2 | 5 ± 0 | 0.12 ± 0.01 | 94 ± 14 | 0.0 ± 0.0 |

- none − headway: +394.6 s/pasager (IÎ 95%: +368.0 … +421.3); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- equalize − headway: +1.8 s/pasager (IÎ 95%: -1.0 … +4.5); mai bun pe 10, mai slab pe 9 din 20 seed-uri
- rl − headway: -7.0 s/pasager (IÎ 95%: -8.9 … -5.2); mai bun pe 19, mai slab pe 1 din 20 seed-uri

## 5 autobuze, doar zgomot

| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |
|---|---|---|---|---|---|---|
| none | 250 ± 39 | 250 ± 39 | 79 ± 38 | 0.66 ± 0.18 | 0 ± 0 | 41.8 ± 14.4 |
| headway | 178 ± 2 | 173 ± 1 | 1 ± 0 | 0.07 ± 0.01 | 28 ± 9 | 0.0 ± 0.0 |
| equalize | 183 ± 4 | 176 ± 2 | 3 ± 1 | 0.12 ± 0.02 | 46 ± 17 | 0.0 ± 0.0 |
| rl | 176 ± 1 | 173 ± 0 | 0 ± 0 | 0.05 ± 0.00 | 20 ± 6 | 0.0 ± 0.0 |

- none − headway: +72.4 s/pasager (IÎ 95%: +54.7 … +90.0); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- equalize − headway: +5.1 s/pasager (IÎ 95%: +3.3 … +6.9); mai bun pe 3, mai slab pe 17 din 20 seed-uri
- rl − headway: -2.0 s/pasager (IÎ 95%: -3.2 … -0.9); mai bun pe 16, mai slab pe 3 din 20 seed-uri

## 5 autobuze, un autobuz oprit 5 min

| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |
|---|---|---|---|---|---|---|
| none | 693 ± 63 | 693 ± 63 | 502 ± 60 | 1.62 ± 0.09 | 0 ± 0 | 94.2 ± 0.1 |
| headway | 197 ± 2 | 181 ± 1 | 4 ± 0 | 0.15 ± 0.01 | 107 ± 10 | 0.0 ± 0.0 |
| equalize | 206 ± 3 | 185 ± 1 | 7 ± 0 | 0.20 ± 0.01 | 137 ± 11 | 0.0 ± 0.0 |
| rl | 196 ± 3 | 180 ± 1 | 4 ± 0 | 0.15 ± 0.01 | 103 ± 11 | 0.0 ± 0.0 |

- none − headway: +496.5 s/pasager (IÎ 95%: +467.3 … +525.7); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- equalize − headway: +9.1 s/pasager (IÎ 95%: +7.5 … +10.7); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- rl − headway: -0.8 s/pasager (IÎ 95%: -2.4 … +0.8); mai bun pe 11, mai slab pe 9 din 20 seed-uri

## 8 autobuze, doar zgomot

| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |
|---|---|---|---|---|---|---|
| none | 194 ± 33 | 194 ± 33 | 92 ± 33 | 0.93 ± 0.17 | 0 ± 0 | 60.1 ± 8.1 |
| headway | 108 ± 2 | 104 ± 0 | 1 ± 0 | 0.09 ± 0.01 | 32 ± 10 | 0.0 ± 0.0 |
| equalize | 112 ± 5 | 106 ± 2 | 3 ± 1 | 0.16 ± 0.04 | 43 ± 19 | 0.0 ± 0.0 |
| rl | 107 ± 1 | 103 ± 0 | 0 ± 0 | 0.05 ± 0.00 | 27 ± 4 | 0.0 ± 0.0 |

- none − headway: +86.0 s/pasager (IÎ 95%: +70.9 … +101.1); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- equalize − headway: +3.8 s/pasager (IÎ 95%: +1.8 … +5.8); mai bun pe 4, mai slab pe 16 din 20 seed-uri
- rl − headway: -1.3 s/pasager (IÎ 95%: -2.3 … -0.3); mai bun pe 15, mai slab pe 4 din 20 seed-uri

## 8 autobuze, un autobuz oprit 5 min

| strategie | întârziere / pasager (s) | așteptare medie (s) | așteptare în exces (s) | CV headway | holding (s / autobuz·h) | timp în bunching (%) |
|---|---|---|---|---|---|---|
| none | 591 ± 61 | 591 ± 61 | 477 ± 59 | 2.05 ± 0.11 | 0 ± 0 | 94.7 ± 0.1 |
| headway | 124 ± 2 | 108 ± 0 | 3 ± 0 | 0.18 ± 0.01 | 108 ± 8 | 2.0 ± 0.1 |
| equalize | 143 ± 2 | 118 ± 1 | 12 ± 0 | 0.33 ± 0.01 | 178 ± 11 | 2.1 ± 0.2 |
| rl | 135 ± 2 | 112 ± 1 | 6 ± 0 | 0.24 ± 0.01 | 159 ± 11 | 2.2 ± 0.3 |

- none − headway: +467.1 s/pasager (IÎ 95%: +438.4 … +495.8); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- equalize − headway: +19.1 s/pasager (IÎ 95%: +17.7 … +20.6); mai bun pe 0, mai slab pe 20 din 20 seed-uri
- rl − headway: +11.7 s/pasager (IÎ 95%: +10.4 … +13.0); mai bun pe 0, mai slab pe 20 din 20 seed-uri

## Scenarii what-if (holding clasic activ)

| scenariu | autobuze | headway mediu (s) | așteptare medie (s) | întârziere / pasager (s) | CV headway | opriri sărite |
|---|---|---|---|---|---|---|
| bază: 5 autobuze | 5→5 | 345 ± 1 | 173 ± 1 | 178 ± 2 | 0.07 ± 0.01 | 0 ± 0 |
| +1 autobuz de la început | 5→6 | 285 ± 1 | 144 ± 1 | 152 ± 2 | 0.11 ± 0.01 | 0 ± 0 |
| +2 autobuze după 1 h | 5→7 | 261 ± 1 | 134 ± 0 | 144 ± 2 | 0.17 ± 0.00 | 0 ± 0 |
| frecvență: un autobuz la 6 min | 5→5 | 345 ± 1 | 173 ± 1 | 178 ± 2 | 0.07 ± 0.01 | 0 ± 0 |
| frecvență: un autobuz la 10 min | 4→4 | 444 ± 2 | 225 ± 2 | 231 ± 4 | 0.10 ± 0.02 | 0 ± 0 |
| stradă închisă km 1,5–2,5, ocol +1,2 km | 5→5 | 452 ± 2 | 227 ± 1 | 352 ± 5 | 0.07 ± 0.00 | 175 ± 1 |
| stradă închisă 1 h (de la 1 h), ocol +1,2 km | 5→5 | 391 ± 1 | 310 ± 2 | 346 ± 4 | 0.78 ± 0.01 | 44 ± 1 |

