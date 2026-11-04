# ADR-003: Istoric de poziții eșantionat în PostgreSQL

- **Stare:** Acceptat
- **Data:** 2026-11-04

## Context
Pentru replay și pentru analiza bus bunching (etapa 4) avem nevoie de traiectoriile
autobuzelor în timp. Simulatorul trimite ~2 poziții/s pentru fiecare dintre cele ~171 de
autobuze, adică ~340 rânduri/s (~29 milioane/zi). PostgreSQL rulează pe Supabase free (500 MB)
și local.

## Decizie
- Backend-ul păstrează **cel mult un rând per vehicul la 5 s** (`HistorySampler`), adică
  ~35 rânduri/s pentru toată flota.
- Scrierea e decuplată de consumatorul Kafka printr-o coadă mărginită (`HistoryQueue`, 20 000
  de elemente, la depășire aruncă cele mai vechi). Inserarea se face în loturi la 2 s
  (`HistoryWriter`).
- Tabelul `VehiclePositionHistory` are indecși pe `(VehicleId, RecordedAt)`, `(RouteId,
  RecordedAt)` și `RecordedAt`.
- Retenție: 3 zile, configurabilă (`Kafka:HistoryRetentionDays`), cu ștergere zilnică.
- Replay: `GET /api/history/positions` (doar Admin), fereastră de cel mult o oră.

## Consecințe
- (+) Volum controlat (~3 milioane rânduri/zi), suficient pentru headway-uri la nivel de
  minut.
- (+) O bază de date lentă nu blochează harta live (coada absoarbe vârfurile).
- (−) Rezoluția de 5 s nu surprinde opririle foarte scurte. Pentru analize fine se poate
  reciti stream-ul Kafka (retenția brokerului) sau scădea intervalul.
- (−) Pentru volume mult mai mari ar fi potrivită o bază de serii temporale (TimescaleDB);
  decizia se poate revizui în etapa 5.
