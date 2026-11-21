# Rezultate: toleranță la erori

Generat cu `tools/resilience/run-fault-tests.sh` pe lanțul complet din `docker compose` (MQTT → ingestie → Kafka → backend → SignalR).
Flotă sintetică de 170 autobuze la 500 ms (MQTT QoS 0), 120 s per experiment; defecțiunea începe la secunda 30.
„Pierdute” = poziții trimise care nu au ajuns pe hartă (SignalR). Întreruperea = secunde cu sub jumătate din debitul așteptat; revenirea = secunde de la defecțiune până la debit ≥ 80%.

| experiment | trimise | pe hartă | pierdute | întrerupere | revenire după | latență p95 maximă | erori la publicare |
|---|---|---|---|---|---|---|---|
| fără defecțiune (referință) | 40800 | 40800 | 0% | 0 s | — | 1.1 s | 0 |
| Kafka (Redpanda) oprit 30 s | 40800 | 40800 | 0% | 30 s | 30 s | 29.9 s | 0 |
| brokerul MQTT repornit | 40800 | 39950 | 2.08% | 2 s | 2 s | 0.2 s | 0 |
| backend-ul repornit | 40800 | 40299 | 1.23% | 1 s | 1 s | 1.6 s | 0 |
| ingestia oprită 30 s (o singură instanță) | 40800 | 30260 | 25.83% | 31 s | 31 s | 0.9 s | 0 |
| una din două instanțe de ingestie oprită (abonament MQTT partajat) | 40800 | 40800 | 0% | 0 s | — | 0.5 s | 0 |

Interpretarea (în engleză) e în documentul proiectului, secțiunea „Fault-tolerance testing” (`tools/report`). Pe scurt: Kafka oprit nu pierde nimic (producătorul păstrează mesajele până la 120 s); cu QoS 0 se pierde ce se publică cât brokerul sau singura instanță de ingestie lipsesc; două instanțe în abonament partajat elimină întreruperea.
