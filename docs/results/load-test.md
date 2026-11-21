# Rezultate: teste de încărcare

Generat cu `tools/loadtest` pe lanțul din `docker compose` (rezultatele brute: `load-*.json`). O flotă sintetică
raportează la 0,5 s; latența e măsurată **cap-coadă**, de la trimiterea poziției până la sosirea ei pe hub-ul SignalR
(cum o vede harta). Potrivirea se face exact, printr-un număr de secvență per mesaj.

**Mediu:** o singură mașină cu 4 vCPU și 15 GB RAM. Toate containerele (Postgres, Mosquitto, Redpanda, colector
OpenTelemetry, Jaeger, Prometheus, ingestie, backend) și generatorul de încărcare rulează pe ea. Cifrele sunt limitele
acestei configurații de test, nu ale unei implementări de producție. Flota Tursib are ~170 de autobuze.

## Ingest HTTP (simulator → backend → hartă)

| autobuze | mesaje/s trimise | ajunse pe hartă | pierdute | latență p50 | p95 | p99 | max |
|---|---|---|---|---|---|---|---|
| 170 | 340 | 340/s | 0% | 5 ms | 13 ms | 128 ms | 130 ms |
| 500 | 1 000 | 1 000/s | 0% | 8 ms | 12 ms | 24 ms | 25 ms |
| 1 000 | 2 000 | 2 000/s | 0% | 12 ms | 19 ms | 28 ms | 28 ms |
| 2 000 | 4 000 | 4 000/s | 0% | 23 ms | 32 ms | 36 ms | 36 ms |
| 5 000 | 10 000 | 10 000/s | 0% | 30 ms | 78 ms | 100 ms | 112 ms |
| 10 000 | 20 000 | 20 000/s | 0% | 48 ms | 88 ms | 139 ms | 144 ms |

## Lanțul complet (dispozitiv → MQTT → ingestie → Kafka → backend → hartă)

| autobuze | mesaje/s trimise | ajunse pe hartă | pierdute | latență p50 | p95 | p99 | max |
|---|---|---|---|---|---|---|---|
| 170 | 340 | 340/s | 0% | 127 ms | 131 ms | 154 ms | 155 ms |
| 500 | 1 000 | 1 000/s | 0% | 128 ms | 130 ms | 134 ms | 135 ms |
| 1 000 | 2 000 | 2 000/s | 0% | 48 ms | 135 ms | 142 ms | 143 ms |
| 2 000 | 4 000 | 4 000/s | 0% | 53 ms | 165 ms | 177 ms | 189 ms |
| 5 000 | 10 000 | 10 000/s | 0% | 114 ms | 252 ms | 294 ms | 365 ms |
| 10 000 | 20 000 | 19 753/s | **1,2%** | 207 ms | 375 ms | 490 ms | 661 ms |

## Ce am învățat

- **Un defect găsit de testul de încărcare.** Prima rulare a lanțului MQTT pierdea **peste 94%** din poziții chiar și la
  170 de autobuze, cu întârzieri de 17–30 s. Ingestia aștepta confirmarea Kafka pentru fiecare mesaj
  (`await ProduceAsync`), iar clientul MQTT livrează mesajele unul câte unul. Debitul era deci plafonat la ~1/latența
  confirmării, adică sub 50 de mesaje/s. Soluția: producție fără blocare, cu confirmarea într-un callback. Ordinea per
  vehicul rămâne garantată de producătorul idempotent, iar eșecurile se numără. După reparație: 0% pierderi până la
  10 000 de mesaje/s.
- **Latența minimă de ~130 ms pe lanțul Kafka e o alegere de configurare, nu o limită.** Producătorul grupează mesajele
  20 ms (`LingerMs`), iar consumatorul așteaptă până la 100 ms ca să umple un lot. La încărcare mică, loturile se închid
  pe timp (~120 ms); la încărcare mare se umplu mai repede și p50 scade (48 ms la 2000 de autobuze). Pentru o hartă care
  se actualizează la 0,5–10 s, 130 ms e neglijabil; pentru latență minimă se pot scădea cele două valori.
- **Marjă:** la flota Tursib (170 de autobuze), lanțul complet are o marjă de ~30× până la primele pierderi
  (10 000 de autobuze, 20 000 de mesaje/s pe această mașină), iar ingestul HTTP una de peste 60×.
- **Primul punct de saturare** e lanțul MQTT → Kafka la 20 000 de mesaje/s. Ingestia (un singur proces) și brokerul
  MQTT împart cele 4 nuclee cu restul stack-ului. Scalarea orizontală e pregătită: abonament MQTT partajat
  (`Mqtt__SharedGroup`) cu mai multe instanțe de ingestie și 3 partiții Kafka.
