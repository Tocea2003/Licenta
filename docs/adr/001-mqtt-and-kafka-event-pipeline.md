# ADR-001: MQTT pentru dispozitive, Kafka (Redpanda) ca log de evenimente

- **Stare:** Acceptat
- **Data:** 2026-11-04

## Context
Până la etapa 2, simulatorul scria pozițiile direct în Firebase Realtime Database, iar
frontend-ul le citea de acolo. Backend-ul nu vedea datele live, deci nu putea:
- să le valideze;
- să le păstreze pentru replay;
- să publice un feed standard;
- să calculeze headway-ul (etapa 4).

Un sistem real de urmărire primește date de la dispozitive montate în autobuze, pe conexiuni
mobile nesigure. Mai mulți consumatori independenți au nevoie de aceleași evenimente: hartă,
istoric, digital twin, controlul bus bunching.

## Decizie
- **MQTT** (Mosquitto) e protocolul dispozitivelor. E standardul de facto pentru IoT: mesaje
  mici, QoS 1, reconectare. Fiecare vehicul publică pe
  `tursib/vehicles/{routeId}/{vehicleId}/position`.
- Un **serviciu de ingestie** separat (`TursibIngestion`):
  - preia mesajele MQTT și le validează (schemă, zona deservită, viteză, timp, potrivire topic/payload);
  - le scrie în **Kafka**, pe topicul `vehicle-positions`, cu cheia = id-ul vehiculului
    (ordinea per autobuz e garantată în partiție);
  - trimite mesajele respinse în `vehicle-positions-dlq`, cu motivul în header.
- **Redpanda** în loc de Apache Kafka: același protocol, un singur binar, fără ZooKeeper/KRaft
  de configurat, potrivit pentru rulare locală pe un laptop.
- Contractul (`Tursib.Contracts`) e o bibliotecă comună pentru simulator, ingestie și backend.
  JSON-ul păstrează numele câmpurilor pe care frontend-ul le citea deja din Firebase.

## Consecințe
- (+) Consumatorii sunt decuplați: SignalR, istoricul, digital twin-ul și controlul pot citi
  același stream în ritmul lor, fiecare cu propriul consumer group.
- (+) Stream-ul poate fi reluat (retenția Kafka), util pentru experimentele din etapele 3–4.
- (+) Datele invalide sunt oprite la intrare și rămân inspectabile în DLQ.
- (−) Trei componente noi de operat (broker MQTT, broker Kafka, ingestie). Rulează doar local
  (`docker compose --profile events`); planul gratuit Render nu le poate găzdui, așa că
  producția rămâne pe Firebase (ADR-002).
- (−) MQTT rulează deocamdată anonim, fără TLS. Autentificarea dispozitivelor e datorie
  tehnică pentru etapa 5.
