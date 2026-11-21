# ADR-003: Istoric de poziții eșantionat în PostgreSQL

- **Stare:** Acceptat
- **Data:** 2026-11-17

## Context
Replay-ul și analiza bunching-ului au nevoie de traiectoriile autobuzelor în timp. La ~60 de autobuze care raportează
de două ori pe secundă ar fi ~10 milioane de rânduri pe zi, mult peste planul gratuit Supabase (500 MB).

## Decizie
- Un handler pe magistrala de evenimente (`PositionHistoryHandler`) păstrează **cel mult o poziție per autobuz la
  `History:SampleSeconds`**: 15 s implicit, 30 s în cloud (`render.yaml`).
- Scrierea e decuplată de fluxul live printr-un tampon mărginit (`PositionHistoryBuffer`, se pierd cele mai vechi la
  depășire). `PositionHistoryWriter` inserează în loturi și șterge ce e mai vechi decât `History:RetentionDays`.
- Tabelul `VehiclePositionHistory` are indecși pe `(RouteId, RecordedAt)`, `(VehicleId, RecordedAt)` și `RecordedAt`.
- Replay: `GET /api/live/history` și `/history/bounds`, **doar pentru administratori**, fereastră de cel mult 6 h,
  20 000 de rânduri.

## Consecințe
- (+) Volum previzibil: 60 de autobuze la 30 s, păstrate 2 zile ≈ 350 000 de rânduri.
- (+) O bază lentă nu blochează harta live.
- (−) Rezoluția de 15–30 s nu surprinde opririle scurte. Pentru analize fine se pot scădea intervalul sau reciti
  fluxul Kafka cât timp e în retenție.
