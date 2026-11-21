# ADR-008: Holding clasic vs Q-learning tabular, comparate în twin

- **Stare:** Acceptat
- **Data:** 2026-11-21

## Context
Obiectivul de cercetare cere compararea unei strategii clasice de holding cu una bazată pe reinforcement learning, după
timpul de așteptare și varianța headway-ului.

## Decizie
- **Holding clasic** (`HoldPolicy`): la sosirea în stație, un autobuz prea apropiat de cel din față (sub 0,85 din golul
  ideal) e reținut cât să recupereze golul lipsă (`headway`) sau să egalizeze față/spate (`equalize`), cel mult 45 s
  într-o stație. Aceeași politică dă recomandările live pentru dispecer.
- **RL** (`RlHoldControl`): Q-learning tabular, cu o politică comună tuturor autobuzelor.
  - Starea e (gol față, gol spate) raportate la ideal, câte 8 intervale fiecare. Acțiunile sunt reținere de
    0, 5, 15, 30 sau 45 s.
  - Recompensa e −(3 × așteptarea în exces produsă la stația următoare + întârzierea pasagerilor din autobuz din cauza
    reținerii), în pasager·secunde. Actualizarea e SMDP, la următoarea decizie a aceluiași autobuz.
  - Explorarea scade de la ε 0,3 la 0,02, iar rata de învățare de la 0,2 la 0,02. Antrenarea se face pe scenarii
    aleatoare: 3–8 autobuze, linii de 3–8 km, cerere variabilă, perturbări.
- **Variabilitatea antrenării** e reală: politici antrenate cu seed-uri diferite diferă. Antrenăm 5 candidați și îl
  păstrăm pe cel mai bun pe un **set de validare**. Rezultatele se raportează pe un **set de test** separat, cu interval
  de încredere pe diferența pereche față de holding (`TwinExperiment`, `docs/results/`).
- Tabelar, nu rețea neuronală: 64 × 5 valori, interpretabil, fără dependențe ML, antrenat în ~3 minute.

## Consecințe
- (+) Comparație corectă metodologic: aceleași scenarii și seed-uri, seturi disjuncte de antrenare, validare și test.
- (+) Politica e un fișier JSON încorporat în backend (`Resources/rl-hold-policy.json`), regenerabil
  (`dotnet run -- train-rl`).
- (−) Starea nu include încărcătura, cererea sau ora din zi. O aproximare de funcție (DQN) ar putea folosi mai multă
  informație.
- (−) Rezultatele sunt din model, nu din teren.
