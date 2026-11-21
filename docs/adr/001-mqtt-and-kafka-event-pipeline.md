# ADR-001: MQTT pentru dispozitive, Kafka (Redpanda) ca flux de evenimente

- **Stare:** Acceptat
- **Data:** 2026-11-17

## Context
Prototipul de licență scria pozițiile direct din simulator în Firebase Realtime Database, iar frontend-ul le citea de
acolo. Backend-ul nu vedea datele live, deci nu le putea valida, nu le putea păstra pentru replay, nu putea publica un
feed standard și nu putea măsura headway-ul. Un sistem real primește date de la dispozitive montate în autobuze, pe
conexiuni mobile nesigure, și are mai mulți consumatori independenți ai acelorași evenimente: hartă, istoric, feed
GTFS-RT, detecția bunching, digital twin.

## Decizie
- **MQTT** (Mosquitto) e protocolul dispozitivelor: mesaje mici, QoS, reconectare. Fiecare vehicul publică pe
  `tursib/vehicles/{vehicleId}/position`. Id-ul vehiculului se ia **din topic**; un payload cu alt `id` e respins
  (`IdMismatch`), deci un dispozitiv nu poate raporta în numele altui autobuz. În producție, ACL-ul brokerului leagă
  topicul de dispozitiv (`infra/mosquitto/mosquitto.conf`).
- Un **serviciu de ingestie** separat (`TursibIngestion`) validează mesajele (`PositionParser`: dimensiune, JSON,
  topic, id, linie, coordonate, timp) și le scrie pe topicul Kafka `vehicle-positions`, cu **cheia = id-ul vehiculului**,
  deci ordinea pozițiilor unui autobuz se păstrează în partiție. Mesajele respinse merg în `vehicle-positions.dlq`, cu
  motivul în antetul `reason`. Producătorul e idempotent.
- **Redpanda** local (compatibil Kafka, un singur proces); Apache Kafka și orice serviciu gestionat (SASL_SSL) merg la
  fel, prin `KafkaConnectionOptions`.
- Contractul mesajului și validarea stau în biblioteca comună `TursibMessaging`, folosită de worker și de backend.
- Backend-ul consumă cu **commit după aplicare** (cel puțin o dată). Starea live ignoră pozițiile mai vechi sau egale,
  deci reprocesarea e inofensivă.
- Topicurile au retenție de 1 h: Kafka e transportul live, istoricul pentru replay stă în PostgreSQL (ADR-003).

## Consecințe
- (+) Consumatorii sunt decuplați: fiecare citește același flux în ritmul lui (`IBusPositionHandler`).
- (+) Datele invalide sunt oprite la intrare și rămân inspectabile în DLQ.
- (+) Scalare orizontală: partiții Kafka și abonamente MQTT partajate (`Mqtt__SharedGroup`).
- (−) Trei componente noi de operat (broker MQTT, broker Kafka, worker). Nu încap în planul gratuit Render (ADR-004).
- (−) Două definiții ale poziției (mesajul de pe fir și `BusPosition` din backend); un test de contract le ține
  sincronizate.
