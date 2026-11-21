# ADR-004: Ingestie MQTT integrată în backend pentru planul gratuit

- **Stare:** Acceptat
- **Data:** 2026-11-17

## Context
Planul gratuit Render are ~750 de ore de instanță pe lună pentru tot workspace-ul. Backend-ul și simulatorul consumă
deja ~1440 h dacă stau mereu pornite. Un worker de ingestie plus un cluster Kafka nu încap.

## Decizie
Backend-ul poate rula ingestia MQTT **în proces** (`Ingestion:EmbeddedMqtt=true`, `EmbeddedMqttIngest`), cu aceeași
validare (`PositionParser`) ca worker-ul, grupând mesajele la 200 ms într-un singur lot. Brokerul MQTT e gestionat
(HiveMQ Cloud, TLS + parolă). Lanțul complet cu Kafka rămâne varianta pentru `docker compose` sau un server propriu.
Ingestul HTTP (`POST /api/live/positions`, cheie API) merge în orice mod.

## Consecințe
- (+) Același cod de validare și același punct de intrare (`LiveIngestService`) în toate modurile de implementare.
- (−) Fără log de evenimente durabil în cloud: un backend oprit pierde mesajele din acel interval (QoS 0).
- (−) Serviciile gratuite adorm după ~15 minute fără trafic HTTP; soluții în `docs/DEPLOYMENT.md` §5.
