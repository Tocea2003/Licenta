# Punere în producție: broker MQTT, GTFS pe Supabase și restul

Ghid pas cu pas pentru varianta din cloud (planul gratuit Render + Supabase + Vercel). Pașii marcați **(tu)** cer
conturile tale; restul e deja pregătit în cod.

```
BusSimulator (Render)  --MQTT, TLS 8883-->  broker gestionat (HiveMQ Cloud)  -->  TursibBackend (Render)
                                                                                    ├─ SignalR → harta (Vercel)
                                                                                    ├─ istoric → Supabase (replay)
                                                                                    └─ GTFS-RT, bunching, twin
GTFS oficial (tursib.ro) --GitHub Actions--> Supabase (rute, stații, curse, calendar)
```

De ce fără Kafka în cloud: planul gratuit Render are ~750 de ore de instanță pe lună pentru tot workspace-ul, deci nu
încape un al treilea serviciu mereu pornit (worker-ul de ingestie) plus un cluster Kafka. Backend-ul rulează ingestia
MQTT integrată, cu aceeași validare ca worker-ul. Lanțul complet cu Kafka rulează cu `docker compose up` (pasul 6).

Ordinea recomandată: **1 → 2 → 3 → 4** (GTFS-ul poate fi făcut și primul; e independent).

---

## 0. O singură dată, înainte de primul deploy de pe această ramură

Backend-ul folosește acum biblioteca comună `TursibMessaging`, deci imaginea Docker se construiește din **rădăcina
repo-ului**. `render.yaml` are deja `dockerContext: .`.

- Dacă serviciile Render au fost create **din Blueprint** (`render.yaml`): la sincronizarea Blueprint-ului setările
  se aplică singure.
- Dacă serviciul backend a fost creat **manual** **(tu)**: Render → serviciul backend → *Settings* → *Build & Deploy*:
  - *Root Directory*: gol
  - *Dockerfile Path*: `TursibBackend/Dockerfile`
  - *Docker Build Context Directory*: `.`

Fără asta build-ul eșuează. Render păstrează însă versiunea veche online, deci nu pică nimic.

---

## 1. Brokerul MQTT (HiveMQ Cloud, gratuit) **(tu)**

Orice broker MQTT cu TLS și utilizator/parolă merge: EMQX Cloud, un Mosquitto propriu etc. Mai jos e HiveMQ Cloud.
Etichetele din interfața lor se pot schimba; caută echivalentul.

1. Creează cont pe https://console.hivemq.cloud și un cluster **Serverless** (gratuit). Alege o regiune din UE.
2. Din pagina clusterului notează **URL-ul** (ceva de forma `xxxxxxxx.s1.eu.hivemq.cloud`) și portul **8883** (TLS).
3. *Access Management* → creează credențiale. Recomandat, două:
   - `tursib-sim`: folosit de simulator (publică);
   - `tursib-backend`: folosit de backend (se abonează).
   Dacă planul permite permisiuni pe topic, restrânge-le la `tursib/vehicles/#`.
4. Verifică brokerul de pe calculatorul tău (opțional, cu `mosquitto-clients`):
   ```bash
   # terminal 1: ascultă
   mosquitto_sub -h <host> -p 8883 --capath /etc/ssl/certs -u tursib-backend -P '<parola>' -t 'tursib/vehicles/+/position' -v
   # terminal 2: publică un mesaj de test
   mosquitto_pub -h <host> -p 8883 --capath /etc/ssl/certs -u tursib-sim -P '<parola>' \
     -t tursib/vehicles/route_1_bus_0/position -m '{"latitude":45.79,"longitude":24.15,"routeId":1}'
   ```
   Pe macOS, în loc de `--capath /etc/ssl/certs` folosește `--cafile /etc/ssl/cert.pem`.

**Traficul contează.** Planurile gratuite au trafic lunar limitat (la HiveMQ Serverless era ~10 GB/lună la ultima
verificare; vezi pagina lor de prețuri). Un mesaj are ~400 B, și se numără și intrarea, și ieșirea:

| raportare per autobuz | ~60 autobuze (3 pe linie), 12 h/zi, 22 zile |
|---|---|
| 0,5 s (implicit local) | ~90 GB/lună: prea mult |
| **10 s (setat în `render.yaml`)** | **~4,5 GB/lună** |
| 30 s | ~1,5 GB/lună |

`MQTT_PUBLISH_INTERVAL_SECONDS` din simulator controlează asta. Dispozitivele AVL reale raportează și ele la 10–30 s.

---

## 2. Variabilele de mediu în Render **(tu)**

Render → serviciul → *Environment*. Valorile de tip `sync: false` din `render.yaml` se completează aici; celelalte
sunt deja setate dacă folosești Blueprint-ul (altfel adaugă-le manual, cu valorile de mai jos).

**Backend** (`tursib-backend`):

| variabilă | valoare |
|---|---|
| `Ingestion__EmbeddedMqtt` | `true` |
| `Mqtt__Host` | host-ul brokerului (fără `mqtts://`, fără port) |
| `Mqtt__Port` | `8883` |
| `Mqtt__UseTls` | `true` |
| `Mqtt__Username` / `Mqtt__Password` | `tursib-backend` / parola lui |
| `History__Enabled` | `true` (replay) |
| `History__SampleSeconds` | `30` |
| `History__RetentionDays` | `2` |

Istoricul la 30 s, păstrat 2 zile, înseamnă cel mult ~350 k rânduri (~60 de autobuze, non-stop), adică zeci de MB
cu tot cu indecși. Încape lejer în planul gratuit Supabase (500 MB). Cu `History__Enabled=false` pagina de replay spune că istoricul e oprit.

**Simulator** (`tursib-bus-simulator`):

| variabilă | valoare |
|---|---|
| `MQTT_HOST` | același host |
| `MQTT_PORT` | `8883` |
| `MQTT_TLS` | `true` |
| `MQTT_USERNAME` / `MQTT_PASSWORD` | `tursib-sim` / parola lui |
| `MQTT_PUBLISH_INTERVAL_SECONDS` | `10` |
| `FIREBASE_ENABLED` | `false`, ca să nu mai scrie și în Firebase (opțional; altfel rămâne rezervă) |

Fără `MQTT_HOST` simulatorul revine la ingestul HTTP (`INGEST_API_KEY`, aceeași valoare pe ambele servicii), deci
pasul 2 nu strică nimic dacă îl faci pe jumătate.

---

## 3. Verificare

Deschide `https://<backend>.onrender.com/api/live/status` (prima cerere poate dura ~1 min dacă serviciul dormea):

```jsonc
{
  "live":   { "buses": 60, "fresh": 60, ... },        // fresh > 0: sosesc poziții
  "ingest": { "embeddedMqtt": { "connected": true, "received": 1234, "rejected": 0 }, ... },
  "history":{ "enabled": true, "written": 456, "lastError": null, ... },
  "schedule":{ "serviceDate": "2026-11-17", "tripsToday": 1500, "hasCalendar": true }   // după pasul 4
}
```

| simptom | cauză probabilă |
|---|---|
| `embeddedMqtt` e `null` | lipsește `Mqtt__Host` sau `Ingestion__EmbeddedMqtt=true` |
| `connected: false` | host/port greșit, TLS oprit sau parolă greșită (vezi logurile backend: „MQTT indisponibil …”) |
| `received` crește, dar `fresh: 0` | mesajele sunt respinse (`rejected` crește): id-ul din payload nu corespunde topicului, coordonate invalide |
| `received: 0` | simulatorul nu publică: verifică `MQTT_*` și logurile simulatorului („Conectat la brokerul MQTT”) |
| `history.lastError` setat | baza nu acceptă scrieri (conexiune, spațiu) |

Apoi, în aplicație: harta arată autobuzele, iar `/admin/replay` redă istoricul, după câteva minute de date.
Feed-ul GTFS-Realtime e la `https://<backend>.onrender.com/api/gtfs-rt/vehicle-positions?format=json`.

---

## 4. GTFS-ul oficial pe Supabase (GitHub Actions) **(tu: un secret și un click)**

Importul rulează din GitHub, unde `tursib.ro` e accesibil, cu workflow-ul `.github/workflows/import-gtfs.yml`.

1. **Conexiunea.** E aceeași valoare ca `ConnectionStrings__DefaultConnection` din Render (backend → *Environment*);
   copiaz-o de acolo. Trebuie să fie prin **Session pooler** (IPv4), pentru că runner-ele GitHub nu au IPv6. Format Npgsql:
   ```
   Host=aws-0-<regiune>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<parola>;SSL Mode=Require
   ```
   (Supabase → *Connect* → *Session pooler* îți dă un URI `postgresql://…`; transformă-l în formatul de mai sus.)
2. GitHub → repo → *Settings* → *Secrets and variables* → *Actions* → *New repository secret*:
   `SUPABASE_CONNECTION_STRING` = valoarea de mai sus.
3. *Actions* → **Import GTFS into Supabase** → *Run workflow*:
   - întâi cu **dry_run** bifat: descarcă feed-ul, verifică fișierele și afișează câte rânduri are fiecare, fără să
     atingă baza;
   - apoi fără dry_run: importă.
4. Verifică `/api/live/status`: `schedule.hasCalendar: true` și `tripsToday > 0` (în cel mult ~1 minut, fără
   repornire). Planificatorul începe să dea plecări reale, iar GTFS-RT primește `trip_id`. Lista de rute e în cache
   30 de minute; pentru efect imediat, *Manual Deploy → Restart* pe backend.

Ce face importul:

- aplică întâi migrările care lipsesc (exact ce ar face și backend-ul la pornire, de ex. tabelul de istoric);
- înlocuiește rutele, stațiile, cursele, orarul și calendarul **într-o singură tranzacție**: dacă ceva eșuează,
  datele vechi rămân;
- **nu atinge** utilizatorii, biletele, plățile sau favoritele, iar autobuzele înregistrate se reatașează la rute.

Actualizare automată lunară (opțională): *Variables* → `GTFS_AUTO_IMPORT` = `true`.

Alternativ, de pe calculatorul tău:

```bash
cd TursibBackend
dotnet run -- import-gtfs --dry-run https://tursib.ro/trasee/gtfs        # verificare, fără bază de date
ConnectionStrings__DefaultConnection='<conexiunea Supabase>' JWT_KEY=orice-text-lung-de-32-caractere \
  ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile -- import-gtfs https://tursib.ro/trasee/gtfs
```

Atenție: în Development, user-secrets indică tot spre Supabase. Suprascrie conexiunea explicit, ca mai sus.

---

## 5. Să nu adoarmă serviciile **(tu, opțional)**

Serviciile gratuite Render adorm după ~15 minute fără trafic HTTP; un backend sau un simulator adormit oprește
datele live. Cu două servicii ținute mereu treze (2 × 720 h) depășești cele ~750 h gratuite, deci ține-le treze doar
în orele de demo:

- **Recomandat: UptimeRobot** (gratuit, https://uptimerobot.com): două monitoare HTTP la 5 minute, pe
  `https://<backend>.onrender.com/api/live/status` și `https://<simulator>.onrender.com/`, cu *maintenance windows*
  în afara orelor de demo.
- **Sau GitHub Actions**: workflow-ul `keep-alive.yml` e deja în repo. Setează variabila de repo `KEEPALIVE_URLS`
  (cele două URL-uri, separate prin spațiu). Rulează luni–vineri, 08–20 ora României, ~530 h/lună. Cron-ul GitHub
  poate întârzia, iar pe un repo privat consumă din minutele gratuite.

Alternativa fără compromisuri e un plan plătit pentru backend (Render Starter) sau găzduirea pe un VPS cu
`docker compose`.

---

## 6. Lanțul complet cu Kafka (local sau pe un server propriu)

```bash
export JWT_KEY=$(openssl rand -hex 32)
docker compose up -d --build                     # db, mosquitto, redpanda, ingestion, backend, frontend, simulator
docker compose run --rm --no-deps backend import-gtfs https://tursib.ro/trasee/gtfs
curl -s localhost:5022/api/live/status           # ingest.kafka.lastMessageAt se actualizează
```

Cu Kafka gestionat (Redpanda Cloud, Upstash, Confluent), pe worker și pe backend:
`Kafka__BootstrapServers`, `Kafka__SecurityProtocol=SaslSsl`, `Kafka__SaslMechanism=Plain|ScramSha256|ScramSha512`,
`Kafka__SaslUsername`, `Kafka__SaslPassword` (și `Kafka__SslCaLocation` pentru un CA propriu). MQTT pe worker:
`Mqtt__UseTls=true`, `Mqtt__Port=8883`, `Mqtt__Username`, `Mqtt__Password`.

---

## 7. Baza SQLite veche din istoricul git **(tu, decizie)**

`TursibBackend/TursibDb.db` conține conturi reale (parole BCrypt). Nu mai e urmărită în git, dar **rămâne în
commit-urile vechi**. Ca s-o scoți de tot trebuie rescris istoricul, cu `git filter-repo` și force-push pe `main`.
După asta, oricine are o clonă trebuie să cloneze din nou, iar PR-urile deschise trebuie refăcute. Nu am făcut asta
automat, pentru că e ireversibil. Pașii, dacă decizi:

```bash
pip install git-filter-repo
git clone --mirror https://github.com/Tocea2003/licenta.git && cd licenta.git
git filter-repo --invert-paths --path TursibBackend/TursibDb.db --path TursibBackend/TursibDb.db-shm --path TursibBackend/TursibDb.db-wal
git push --force --mirror
```

Dacă repo-ul a fost vreodată public, consideră parolele din acea bază compromise: cere resetarea lor sau invalidează
conturile vechi.
