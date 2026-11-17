# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

TURSIB Tracker: real-time bus tracking and trip planning for Sibiu public transport, built on the real Tursib GTFS feed. Originally a bachelor thesis; now being extended as a master's "Advanced System Engineering" project (event-driven platform, digital twin, bus-bunching control). UI copy is Romanian first, with English and German translations.

Four deployables, one shared library, plus tests:

- `TursibBackend/` – ASP.NET Core 9 REST API (EF Core, JWT auth, GTFS import, routing).
- `TursibFrontend/Frontend/` – Vue 3 + TypeScript + Vite PWA (Leaflet map, Pinia, Capacitor for Android/iOS).
- `BusSimulator/` – .NET console app that simulates buses along GTFS shapes and publishes their positions (MQTT, HTTP ingest and/or Firebase).
- `TursibIngestion/` – .NET worker: MQTT → validation → Kafka topic `vehicle-positions`.
- `TursibMessaging/` – shared class library (message contract + validation `PositionParser`, `MqttSubscriber`/`MqttOptions` with TLS, `KafkaConnectionOptions` with SASL/SSL). Referenced by the backend and the ingestion worker, **so their Docker build context is the repo root** (`docker build -f TursibBackend/Dockerfile .`; same in `docker-compose.yml`, `render.yaml` and the CI matrix; the root `.dockerignore` keeps the context small). The simulator and the frontend keep their own folder as context.
- `TursibBackend.Tests/` – xUnit tests for the backend.

The root `.gitignore` is a **whitelist** (`*` then `!folder/**`). A new top-level file or folder is ignored until it is added there.

## Commands

The machine may only have the .NET 10 runtime while projects target net9.0; prefix `dotnet` commands with `DOTNET_ROLL_FORWARD=Major` if you get "framework not found".

Backend:

```bash
cd TursibBackend && dotnet run                     # API on http://localhost:5022
cd TursibBackend.Tests && dotnet test              # all backend tests
dotnet test TursibBackend.Tests --filter "FullyQualifiedName~RouteCalculatorServiceTests"
dotnet test TursibBackend.Tests --filter "FullyQualifiedName~CorsTests.Preflight_RejectsUnknownOrigins"
```

The API needs `JWT_KEY` (env) or `Jwt:Key` in config. An admin account is bootstrapped at startup from `ADMIN_USERNAME` / `ADMIN_PASSWORD` only when no admin exists; public registration and Google login always create role `User`.

Frontend (run inside `TursibFrontend/Frontend`):

```bash
npm ci
npm run dev                                        # Vite on :5173 (strictPort); API from VITE_API_URL, default http://localhost:5022/api
npx vitest run                                     # all unit tests
npx vitest run src/utils/lineColors.test.ts        # one file
npx vitest run -t "normalizes color and routeColor" # by test name
npx vue-tsc --build                                # type-check
npx oxlint . -D correctness --ignore-path .gitignore
npx eslint .
npx vite build
```

CI (`.github/workflows/ci.yml`) runs backend tests, the simulator build, frontend lint/type-check/tests/build and Docker image builds on PRs and pushes to `main`. Lint must have 0 errors; `no-explicit-any` is only a warning until `MapView.vue` is fully typed.

## Backend architecture

- `Program.cs` wires everything: DbContext, JWT bearer auth, rate limiting (`general` 100/min, `auth` 10/min per IP), an **exact-origin** CORS allow-list (production frontend, Capacitor origins, localhost ports only in Development, extra origins via `CORS_ALLOWED_ORIGINS`), migrations on startup (only for relational providers, so InMemory tests work), and the admin-only `POST /api/import-gtfs`.
- GTFS data (`Routes`, `Stations`, `RouteStations`, `Trips`, `StopTimes`, `Shapes`, `ServiceCalendars`) is loaded by `GTFSImporter`. `RouteStations` is derived: for each route the importer picks a representative trip (most frequent stop count, then earliest departure) and stores its stops in order.
- `Services/RouteCalculatorService.cs` is the routing core and has two planners:
  1. **Schedule-based** (tried first): builds a per-day index of real trips from `StopTimes`, filtered by `ServiceCalendars` and `ServiceCalendarDates` exceptions for that date, and runs a time-dependent earliest-arrival search with walking links between stops within 500 m. Supports depart-at and arrive-by, and enumerates successive real departures.
  2. **Estimated Dijkstra** (fallback when there is no calendar/schedule for that date): graph of consecutive stops per route plus walking edges, travel time from average speed.
  Both are cached in `IMemoryCache` (5 min). Tests in `RouteCalculatorServiceTests` cover both paths; a result starting exactly at the requested time means the estimated fallback ran.
- Ticket purchase uses `PaymentSimulatorService` (Luhn check, special test card numbers trigger declines); no real payment provider.
- Database: PostgreSQL hosted on Supabase (Npgsql provider). All data access is LINQ (no raw SQL), so the InMemory provider works in unit tests. Migrations run on startup. Locally, put the connection string in user-secrets (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project TursibBackend`, Development only) or use a local Postgres (`appsettings.Development.json` points at `Host=localhost;Database=tursib_dev`); production reads `ConnectionStrings__DefaultConnection` on Render. Use the Supabase **Session pooler** host (IPv4); the direct host is IPv6-only.
- Maintenance CLI (runs after migrations, then exits): `dotnet run -- copy-from-sqlite <TursibDb.db>` copies the old SQLite database into an empty target; `dotnet run -- import-gtfs <folder>` re-imports a GTFS feed in one transaction. Both reset Postgres identity sequences after inserting explicit ids.
- Postgres integration tests (`PostgresIntegrationTests`) run only when `TEST_POSTGRES_CONNECTION` is set; CI provides a Postgres service.
- The official feed is at https://tursib.ro/trasee/gtfs (redirects to the current `tursib.gtfs_*.zip`). Active services for a date = `calendar.txt` weekdays in range, minus `calendar_dates.txt` removals, plus additions (`RouteCalculatorService.GetActiveServiceIdsAsync`; in Development `GET /api/debug/services?date=yyyy-MM-dd` shows them). Production (Supabase) has no calendar yet, so it still falls back to estimates until the feed is imported there.
- When importing locally, override `ConnectionStrings__DefaultConnection`: the Development user-secrets point at Supabase production.

## Event-driven pipeline (MQTT → Kafka → backend)

```
BusSimulator (acts as in-bus devices) --MQTT tursib/vehicles/{id}/position--> Mosquitto
  --> TursibIngestion (validate; invalid -> vehicle-positions.dlq with a `reason` header)
  --> Kafka/Redpanda topic vehicle-positions (key = vehicle id, so one bus stays ordered)
  --> TursibBackend KafkaPositionConsumer --> LiveIngestService --> LiveBusState + LiveEventBus
        --> SignalR (map)   --> PositionHistory (PostgreSQL, replay)   --> GTFS-RT feed   --> bunching/twin
```

- **TursibIngestion** (`PositionParser`, `PositionIngestor`, `MqttSubscriber`, `KafkaPositionSink`): the vehicle id comes from the MQTT topic and a payload whose `id` differs is rejected (`IdMismatch`), so a device cannot spoof another bus. Also rejects oversized payloads (4 KB), bad JSON, non-`route_` ids, bad coordinates, timestamps > 5 min in the future. Creates the topics at startup (3 partitions, 1 h retention: live data only; replay history lives in PostgreSQL). Config via env: `Mqtt__Host`, `Mqtt__Port`, `Mqtt__Username/Password`, `Mqtt__SharedGroup` (shared subscription to run several instances), `Kafka__BootstrapServers`, `Kafka__Topic`, `Kafka__DeadLetterTopic`. Logs counters every 15 s.
- **Contract**: `VehiclePositionMessage` + `PositionParser` live in `TursibMessaging` and are used by both the worker and the backend's embedded MQTT ingest. The backend's own `BusPosition` is a separate record (its Kafka/HTTP/SignalR shape); `PositionIngestorTests.WhatIngestionWrites_IsWhatTheBackendReads` and `EmbeddedMqttIngestTests.MapsEveryFieldOfTheMessageToABusPosition` guard that the three stay in sync. Add a field in all of them.
- **Backend consumer** (`Services/KafkaPositionConsumer.cs`): enabled only when `Kafka:BootstrapServers` (or `KAFKA_BOOTSTRAP_SERVERS`) is set. Batches up to 500 messages / 100 ms, commits the offset **after** `LiveIngestService.Ingest` (at-least-once; `LiveBusState` ignores older positions, so replays are harmless), `AutoOffsetReset=Latest`. `POST /api/live/positions` (HTTP ingest) goes through the same `LiveIngestService` and stays the simple path for the cloud deployment, where no MQTT/Kafka is provisioned.
- **History** (`Services/PositionHistory.cs`, table `VehiclePositionHistory`, migration `AddVehiclePositionHistory`): off by default. `History:Enabled=true` registers `PositionHistoryHandler` (an `IBusPositionHandler`) which samples one position per bus every `History:SampleSeconds` (15) into a buffer; `PositionHistoryWriter` flushes it in batches every `History:FlushSeconds` and purges rows older than `History:RetentionDays` (3). Row volume matters on Supabase's free tier: ~60 buses at 15 s ≈ 350 k rows/day. `GET /api/live/history?routeId=|vehicleId=&from=&to=&limit=` returns them chronologically (max 6 h window, 20 000 rows) and `GET /api/live/history/bounds?routeId=` the first/last recorded second (204 when empty); both are rate-limited and answer 503 when disabled. Both are public like the live data; the admin UI that uses them is guarded only on the client.
- **GTFS-Realtime** (`Protos/gtfs-realtime.proto` is the official subset with the official field numbers, compiled by Grpc.Tools; `GtfsRealtimeFeedBuilder`): `GET /api/gtfs-rt/vehicle-positions` (protobuf; `?format=json` for debugging). Vehicles silent for 2 min drop out. `trip_id` comes from `TripMatcher` (below); vehicles with no matching trip stay `UNSCHEDULED` with `route_id` + `direction_id`. Speed is m/s, occupancy % maps to `OccupancyStatus`, `stop_id` is the next station id.

### Trip matching (`trip_id` for GTFS-RT)

Devices only report position, so `Services/TripMatcher.cs` deduces the scheduled trip, as operators do without AVL trip data: among today's trips (active services via `RouteCalculatorService.GetActiveServiceIdsAsync`, Romania local date) on the same `route_id` and direction that stop at the vehicle's `nextStationId`, pick the one whose scheduled departure there is closest to now, within `TripMatching:MaxDeviationSeconds` (30 min). The assignment is **sticky** (kept while the vehicle moves forward on the same trip, same direction and within the deviation limit) and **exclusive** (no two vehicles share a trip). A line with trips in only one direction matches regardless of the reported direction (circular lines). The day's schedule index is built lazily, cached per date (a missing schedule is retried after 5 min), and the feed degrades to `UNSCHEDULED` if anything fails. The simulator does not follow the timetable, so its matches are "best fit", not real; with real AVL data the same code identifies the actual trip. `start_time`/`start_date` and `SCHEDULED` are set for matched vehicles.

### Deployment modes (what runs where)

- **Cloud / free tier (Render)**: simulator → managed MQTT broker (e.g. HiveMQ Cloud free, TLS on 8883) → backend with **embedded ingest** (`Ingestion:EmbeddedMqtt=true` + `Mqtt:Host`, `EmbeddedMqttIngest`: same `PositionParser`, batches every 200 ms into `LiveIngestService`, no Kafka, no worker). Reason: Render's free plan has ~750 instance-hours/month for the whole workspace, so a third always-on service (worker) plus a Kafka cluster does not fit. The backend and simulator `render.yaml` entries carry the `Mqtt__*` / `MQTT_*` variables (`sync: false`: fill them in the Render dashboard with the broker's host and credentials); without `Mqtt__Host` the backend does not start the subscriber, and without `MQTT_HOST` the simulator falls back to HTTP ingest. Free web services still sleep after ~15 min without HTTP traffic; a sleeping backend or simulator stops the data (known limitation; keep-alive pings would eat the hour quota). **The managed-broker part is configured but was not run against a real cloud broker** (no account available in the dev environment); TLS + username/password were verified against a local Mosquitto with its own CA.
- **Full stack (docker-compose / own server)**: simulator → Mosquitto → `TursibIngestion` → Kafka/Redpanda → backend consumer. For managed Kafka (Redpanda Cloud, Upstash, Confluent) set `Kafka__SecurityProtocol=SaslSsl`, `Kafka__SaslMechanism=Plain|ScramSha256|ScramSha512`, `Kafka__SaslUsername/SaslPassword` (and `Kafka__SslCaLocation` for a private CA) on both the worker and the backend; MQTT: `Mqtt__UseTls=true`, `Mqtt__Port=8883`, `Mqtt__Username/Password`, `Mqtt__CaCertificatePath`. Verified against a local Kafka with `SASL_SSL` + PLAIN (and Redpanda v24.3 from Docker Hub for the unsecured path; `docker.redpanda.com` may be blocked by proxies, so compose uses the Docker Hub image).
- HTTP ingest (`POST /api/live/positions`) works in every mode.

Local run without Docker (what was used to verify the chain): Mosquitto (`listener 1883`, `allow_anonymous true`), Kafka/Redpanda on `localhost:9092`, then `Mqtt__Host=127.0.0.1 Kafka__BootstrapServers=localhost:9092 dotnet run --project TursibIngestion`, the backend with `Kafka__BootstrapServers=localhost:9092 History__Enabled=true`, and the simulator with `MQTT_HOST=127.0.0.1 FIREBASE_ENABLED=false`. `docker compose up` starts everything (`mosquitto`, `redpanda`, `ingestion`, backend, simulator). The simulator publishes QoS 0 by default (`MQTT_QOS=1` for at-least-once); `MQTT_PORT/USERNAME/PASSWORD` are supported. Mosquitto config and the production ACL sketch are in `infra/mosquitto/mosquitto.conf`.

Integration tests against real brokers (`EventInfraIntegrationTests`) run only when their env vars are set (CI job `event-infra` sets the first two): `TEST_KAFKA_BOOTSTRAP` + `TEST_MQTT_HOST` (MQTT → ingestion code → Kafka + DLQ; Kafka → backend consumer; MQTT → embedded ingest → snapshot), and for the secured brokers `TEST_MQTT_TLS_PORT`, `TEST_MQTT_CA`, `TEST_SECURE_USER/PASS`, `TEST_KAFKA_SASL_BOOTSTRAP` (TLS + password, wrong password / untrusted certificate refused, Kafka SASL_SSL). Everything else is covered with in-memory fakes.

## Live bus data flow (event-driven, Stage 1)

`BusSimulator` loads routes/stations/shapes from the API and moves `BusesPerRoute` buses per route (dwell at stops, peak-hour speeds in Romania local time). Every ~0.5 s it sends **one batch** with all positions to `POST /api/live/positions` (header `X-Ingest-Key` = `INGEST_API_KEY`, set on both backend and simulator; without it ingest returns 503, and the simulator skips the backend). It still writes `bus_locations/route_{routeId}_bus_{index}` to Firebase unless `FIREBASE_ENABLED=false`.

Backend (`Services/LiveBusService.cs`, `Hubs/BusHub.cs`, `Controllers/LiveController.cs`): `LiveBusState` (singleton, last position per bus; validates, ignores older timestamps, `StaleBusCleaner` drops buses silent for 5 min) → `LiveEventBus` (bounded `Channel`) → `LiveEventDispatcher` calls every registered `IBusPositionHandler`. `HubBroadcastHandler` pushes batches to SignalR clients (`/hubs/buses`, event `positions`; `GetSnapshot()` returns everything on connect). **New consumers (bunching detector, digital twin) = a new `IBusPositionHandler` registered in `Program.cs`.** `GET /api/live/positions` returns the same snapshot over REST.

Frontend `stores/liveBuses.ts` is the only reader (MapView, AdminBuses, AdminAnalytics): SignalR first, Firebase as automatic fallback while SignalR delivers nothing for 15 s (`VITE_LIVE_SOURCE=signalr|firebase|auto`, default `auto`). Never call Firebase `off()` on `bus_locations`; it removes every listener. Entries not starting with `route_` are legacy and ignored.

### Bus-bunching detection (Stage 2)

`Services/BunchingService.cs`: `BunchingDetector` is an `IBusPositionHandler` (throttled to one evaluation per `EvaluationIntervalSeconds`). For each route with ≥2 fresh buses (with `directionId`) it projects every position onto the route shape (`RouteGeometry`, from the first trip's shape via `DbRouteGeometryProvider`, the same shape the simulator drives; a per-bus hint keeps looping routes on the right pass), places the bus on the **round-trip loop** (`BunchingAnalyzer.LoopPosition`: forward = distance, returning = 2L − distance) and measures the gap to the bus ahead against the ideal `2L / n`. `BunchingTracker` turns ratios into alerts: enter below `EnterRatio` (0.35) for `MinDurationSeconds` (10 s), leave above `ExitRatio` (0.6); severity `critical` below 0.15. Thresholds live in the `Bunching` config section (`BunchingOptions`; tests set `Bunching:MinDurationSeconds=0`).

Outputs: SignalR event `bunching` (full list of active alerts, pushed on every change and every evaluation while any is active), hub method `GetBunching()`, and `GET /api/live/bunching` (`{ active, recent }`). Frontend: `stores/liveBuses.ts` exposes `bunching`; `components/BunchingPanel.vue` (in AdminAnalytics) renders it, `utils/bunching.ts` holds the pure helpers.

### Digital twin (Stage 3)

`Services/TwinEngine.cs` is a deterministic (seeded), headless simulation of one line on its round-trip loop (length 2L, stops evenly spaced). Buses cannot overtake (`MinGapMeters`); dwell time at a stop = base + boarding × passengers waiting (arrival rate × time since the previous bus left), with `DwellJitter` noise. That feedback is what makes bunching emerge: calibrated (`PassengerArrivalRate` 0.008/s) so an even fleet without noise stays stable, noise makes it drift, and a stopped bus (`TwinDisturbance`) bunches the fleet. `ITwinControl.ExtraHoldSeconds(TwinStopContext)` is called when a bus arrives at a stop and may add holding time; `NoControl` is the default. **Stage 4 control strategies implement this interface and are compared in the twin before use.**

`TwinRunner.Run` (pure) samples every 10 s and reuses `BunchingAnalyzer`/`BunchingTracker`, returning `TwinRunResult` (`bunchedPercent`, `meanCv`, `minRatio`, `bunchingEpisodes`, 60 s series). `TwinService` adds the real world: geometry from `IRouteGeometryProvider` and, with `UseLiveState`, the starting positions of the live fleet (`initialisedFrom`: `live` or `even`). `POST /api/twin/run` (public, rate-limited, `TwinScenario.Validate` caps: 60 s–6 h, 2–30 buses, ≤10 disturbances) never touches `LiveBusState`. Frontend: `components/TwinPanel.vue` in AdminAnalytics (scenario form, run history, SVG chart of the minimum gap ratio against the 0.35 threshold), pure helpers in `utils/twin.ts`.

### Bunching control (Stage 4)

`Services/HoldControl.cs`: **holding** = a bus that is too close to the one ahead is kept at the stop. `HoldPolicy.HeadwayHold` (hold `(ideal − gapAhead) / v`, i.e. recover the missing gap) and `EqualizeHold` (`(gapBehind − gapAhead) / 2v`, balance front/back), both only below `HoldOptions.TriggerRatio` (0.85) and capped at `MaxHoldSeconds` (45 s per stop); config section `Holding`. They are wrapped as `ITwinControl` (`HeadwayHoldControl`, `EqualizeHoldControl`; `HoldPolicy.Create(name)`; names `none|headway|equalize`). Defaults were chosen from twin experiments (4 h, several seeds, 3–8 buses, noise and a 5 min stop): control removes almost all bunching; an early/light trigger costs less holding than a late/heavy one; `headway` is slightly better than `equalize` on larger fleets. Those are **model results, not field data**.

- Twin: `TwinScenario.Strategy`, `TwinRunResult` now also has `Strategy`, `HoldSecondsPerBusHour` (cost to on-board passengers) and `AverageSpeedKmh`. `POST /api/twin/compare` (`TwinCompareRequest`: scenario + `strategies` + `replications` ≤ 10) runs every strategy over the same consecutive seeds and returns `TwinStrategySummary` averages with a point-wise averaged series.
- Live: `BunchingPair`/`BunchingAlert` carry `RecommendedHoldSeconds` (`HoldPolicy.Recommend` = headway) for the follower of an active alert; `BunchingPanel` shows it as a dispatcher suggestion. Nothing in the backend commands buses.
- Closed loop (demo): with `APPLY_HOLDS=true` the simulator polls `GET /api/live/bunching` every ~5 s (`HoldCoordinator`) and adds the recommended hold to the follower's next stop dwell. Not covered by tests (the simulator has no test project).
- Frontend: `TwinChart.vue` (shared SVG chart), `TwinPanel.vue` "compare strategies" (table, chart, verdict), `utils/twin.ts` (`buildCompareRequest`, `reductionPercent`, `bestStrategy`).

## Frontend architecture

- `views/HomeView.vue` hosts `components/Sidebar.vue` (planner, schedule board, routes list) and `components/MapView.vue` (the map and everything drawn on it). Sidebar emits `routeSelected` / `planSelected`; MapView receives `stations`, `selectedRouteId`, `tripPlan` props and exposes `centerMap`, `invalidateSize`, etc. via `defineExpose`.
- `MapView.vue` is very large and is being split up incrementally. Pure logic already extracted, with tests:
  - `utils/geo.ts` (haversine, bearing, nearest station, trimming a GTFS shape between two stops)
  - `services/osrmService.ts` (public OSRM for walking routes and road fallback) and `services/tripGeometry.ts` (bus segment = GTFS shape trimmed between stops, OSRM fallback)
  - `utils/liveBuses.ts` (parse Firebase data, choose visible buses, station ETAs) and `stores/liveBuses.ts` (one shared, ref-counted subscription: SignalR with Firebase fallback). MapView, AdminBuses and AdminAnalytics all read from it; never call Firebase `off()` on `bus_locations`, it removes every listener including the map's.
  - `services/valhallaService.ts` (walking directions: Valhalla pedestrian profile, localized instructions, straight-line fallback).
- Live bus markers are **imperative Leaflet markers** in a layer group (not vue-leaflet components) so they can animate with a CSS transform transition; the icon is only rebuilt when its visual signature changes. Route casing (white outline) lives in a custom `casingPane` below the overlay pane, because vue-leaflet does not guarantee layer order.
- Map tiles are OpenStreetMap muted with a CSS filter (`.map-tiles`, inverted in dark mode). CARTO basemaps now require an API key, so they are not used.
- `services/apiService.ts` is the public API client. Its response interceptor runs `normalizeLineColorsDeep` (`utils/lineColors.ts`), which rewrites every `color`/`routeColor` hex to a same-hue variant with ≥4.5:1 contrast against white. The admin client (`services/adminService.ts`, which also holds `authService` and the JWT in localStorage) is deliberately not normalized. Cached color/route lists in localStorage use versioned keys (`routeColors.v2`, `routesList.v2`); bump the key if the cached shape or normalization changes.
- Admin replay (`views/AdminReplay.vue` at `/admin/replay`, `components/ReplayMap.vue`, `services/historyService.ts`, pure logic and tests in `utils/replay.ts`): loads a line's history from `/api/live/history` (window presets, or the latest hour that has data via `/history/bounds`), draws the line shape and plays the buses back with linear interpolation between samples (no interpolation across reporting gaps > 90 s), play/pause, 1×/5×/15×/60×, a keyboard-operable slider and short trails. The Leaflet map is imperative (`ReplayMap`), markers are moved, not recreated, on every animation frame. Needs `History:Enabled` on the server.
- Router guards (`router/index.ts`) use `meta.requiresAuth` / `meta.requiresAdmin`; admin routes are lazy-loaded under `/admin`.
- i18n is a hand-rolled dictionary in `composables/useLanguage.ts`: `{ key: { ro, en } }` plus a separate `germanOverrides` map. `t(key, fallback = key, params)` returns the **key itself** when it is missing, so `t('x') || 'default'` never falls back; add the key to the dictionary (and to `germanOverrides`) instead. Duplicate keys break the type-check.
- Dark mode is a `.dark` class on the document (`composables/useDarkMode.ts`), not `prefers-color-scheme`; style dark variants with `.dark`.

## Design system ("Semnal")

The UI is being redesigned to the "Semnal" design (Claude Design canvas: https://claude.ai/artifact/JyzxXR7tRtmG66N9icBptv). Tokens are in `src/assets/main.css` (light + `.dark`): warm paper background, ink text, one cobalt accent (`--accent-primary`), amber `--live`/`--live-soft`/`--live-ink` reserved for live data, Archivo (`--font-display`) for headings, times and line numbers, Public Sans for text, tabular numbers. Old `--gradient-*` tokens resolve to flat colors on purpose.

Conventions used in redesigned components: use the global `.line-pill` (line number on its line color) and `.live-badge` classes; Lucide icons (`lucide-vue-next`) instead of emoji (import Lucide's `Map` as `MapIcon` so it does not shadow the global `Map`); real `<button>`s with `aria-label` for icon-only controls; 44 px minimum touch targets; shared stylesheets for repeated layouts (`assets/trip-panel.css` for trip detail panels, `assets/auth.css` for login/sign-up/admin login). Ticket types and prices live in `constants/ticketCatalog.ts` and must match `TicketsController`. Favorite icons are stored as keys and legacy emoji values are mapped in `utils/favoriteIcons.ts`. Admin table row actions use the shared `assets/admin-actions.css`.

Secondary routes (tickets, settings, favorites, station, …) render inside the `panel-sheet` in `App.vue` over the always-mounted map; `/login`, `/signup`, `/loginadmin` and `/admin/*` are full-page. Some SFCs in this repo put `<script>` before `<template>`; check block order before splicing a file by markers.

## Gotchas

- `vite-plugin-vue-devtools` adds a floating "V" pill at the bottom of the page in dev; it is not part of the app.
- After a Vite hot reload of `MapView.vue` the imperative bus layer can show 0 markers; reload the page before concluding markers are broken.
- A fresh browser context gets one extra page reload on first visit (the PWA service worker); wait for it before scripting the UI. `Map container not found` console errors on `/admin/*` pages come from the always-mounted main map (vue-leaflet) and predate the replay page.
- On Render's free plan the simulator sleeps, so Firebase positions can be days old; check `bus_locations/<id>/timestamp` before debugging "frozen" buses.
- `TursibBackend/TursibDb.db` is the old SQLite database (source for `copy-from-sqlite`) and contains real user accounts; it is no longer used by production and should be removed from git.
