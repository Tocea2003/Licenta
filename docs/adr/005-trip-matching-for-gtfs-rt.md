# ADR-005: `trip_id` dedus din orar pentru GTFS-Realtime

- **Stare:** Acceptat
- **Data:** 2026-11-17

## Context
Consumatorii GTFS-RT (Google Maps, Transit) leagă pozițiile de curse prin `trip_id`. Dispozitivele raportează doar
poziția, iar simulatorul nu urmează curse concrete.

## Decizie
`TripMatcher` alege, dintre cursele care circulă azi (calendar + excepții) pe aceeași linie și direcție și opresc la
următoarea stație a vehiculului, pe cea cu ora de plecare cea mai apropiată de acum, în cel mult 30 de minute. Atribuirea
e **stabilă** (se păstrează cât timp vehiculul înaintează pe aceeași cursă) și **exclusivă** (două vehicule nu primesc
aceeași cursă). Indexul orarului e construit leneș și reîmprospătat (1 min dacă lipsește, 1 h altfel). Vehiculele fără
potrivire rămân `UNSCHEDULED`, cu `route_id` și `direction_id`.

## Consecințe
- (+) Feed valid și util chiar fără date de cursă de la dispozitive; cu date AVL reale, același cod găsește cursa efectivă.
- (−) Pentru simulator, potrivirea e „cea mai plauzibilă”, nu adevărată: simulatorul nu urmează orarul.
