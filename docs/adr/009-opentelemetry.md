# ADR-009: Observabilitate cu OpenTelemetry

- **Stare:** Acceptat
- **Data:** 2026-11-21

## Context
O poziție trece prin cinci componente (dispozitiv, broker MQTT, ingestie, Kafka, backend) și apoi prin mai mulți
consumatori interni. Fără urmărire distribuită, o întârziere sau o pierdere nu se poate localiza. Propunerea cere
observabilitate cu OpenTelemetry.

## Decizie
- Codul folosește API-urile .NET (`ActivitySource`, `Meter`) printr-o singură sursă, `Tursib.Pipeline`
  (`TursibMessaging/Telemetry.cs`). SDK-ul OpenTelemetry din fiecare serviciu le exportă prin **OTLP**, doar dacă e setat
  `OTEL_EXPORTER_OTLP_ENDPOINT`. Local și pe planul gratuit nu costă nimic.
- **Urme:**
  - span-uri: `mqtt receive position`, `kafka produce`, `kafka consume`, `live ingest`, `handle <Handler>`;
  - contextul W3C (`traceparent`) trece prin antetele mesajelor Kafka. Consumatorul procesează loturi, deci span-ul
    `kafka consume` începe o urmă nouă și se leagă de urmele mesajelor din lot prin *span links* (convenția OpenTelemetry
    pentru loturi; cel mult 64 de legături). Din Jaeger se ajunge de la poziție la lotul care a procesat-o;
  - în backend, contextul lotului trece prin magistrala internă (`LiveEvent`), deci `live ingest` și toți handlerii
    (SignalR, bunching, istoric) sunt în aceeași urmă cu consumul.
- **Metrici:**
  - `tursib.positions.received/rejected/accepted`, cu etichete pe etapă, transport și motiv de respingere;
  - `tursib.positions.age`: vârsta poziției la procesare;
  - gauge-uri pentru autobuze live, autobuze proaspete și alerte de bunching active sau critice;
  - instrumentările standard ASP.NET Core, HttpClient și runtime.
- **Eșantionare** `ParentBased(TraceIdRatio)`, cu raportul din `OTEL_TRACES_SAMPLER_ARG` (0,2 în `docker-compose`). La
  20 000 de poziții/s, urmărirea fiecăreia a dus Jaeger la ~4 GB de memorie.
- Local: OpenTelemetry Collector → Jaeger (urme, :16686) și Prometheus (metrici, :9090), în `docker-compose`.

## Consecințe
- (+) Standard deschis: același cod exportă către Grafana, Honeycomb, Azure Monitor etc., schimbând doar colectorul.
- (+) Testele verifică instrumentarea fără exportator, ascultând direct sursa (`TelemetryTests`).
- (−) Fără un ascultător activ nu există span-uri, deci nici context de propagat; e comportamentul .NET și e intenționat.
- (−) Dispozitivele nu trimit context de urmă (MQTT 3.1.1), deci urma începe la ingestie.
