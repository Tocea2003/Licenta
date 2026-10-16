# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

TURSIB Tracker: real-time bus tracking and trip planning for Sibiu public transport, built on the real Tursib GTFS feed. Originally a bachelor thesis; now being extended as a master's "Advanced System Engineering" project (event-driven platform, digital twin, bus-bunching control). UI copy is Romanian first, with English and German translations.

Three deployables plus tests:

- `TursibBackend/` – ASP.NET Core 9 REST API (EF Core, JWT auth, GTFS import, routing).
- `TursibFrontend/Frontend/` – Vue 3 + TypeScript + Vite PWA (Leaflet map, Pinia, Capacitor for Android/iOS).
- `BusSimulator/` – .NET console app that simulates buses along GTFS shapes and writes positions to Firebase.
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
  1. **Schedule-based** (tried first): builds a per-day index of real trips from `StopTimes`, filtered by `ServiceCalendars` for that weekday/date, and runs a time-dependent earliest-arrival search with walking links between stops within 500 m. Supports depart-at and arrive-by, and enumerates successive real departures.
  2. **Estimated Dijkstra** (fallback when there is no calendar/schedule for that date): graph of consecutive stops per route plus walking edges, travel time from average speed.
  Both are cached in `IMemoryCache` (5 min). Tests in `RouteCalculatorServiceTests` cover both paths; a result starting exactly at the requested time means the estimated fallback ran.
- Ticket purchase uses `PaymentSimulatorService` (Luhn check, special test card numbers trigger declines); no real payment provider.
- Database: SQLite (`TursibDb.db`, bundled into the Docker image) on `main`. A migration to PostgreSQL/Supabase is in progress on branch `stage0-postgres` (PR #15): Npgsql provider, all queries as LINQ (no raw SQLite SQL), CLI commands `dotnet run -- copy-from-sqlite <db>` and `dotnet run -- import-gtfs <folder>`, and Postgres integration tests that run when `TEST_POSTGRES_CONNECTION` is set. Check which branch you are on before touching data access.

## Live bus data flow

`BusSimulator` loads routes/stations/shapes from the API, moves `BusesPerRoute` buses per route (dwell at stops, peak-hour speeds in Romania local time) and `PUT`s each position every ~0.5 s to Firebase Realtime Database at `bus_locations/route_{routeId}_bus_{index}`. The frontend reads that node directly; the backend is not in this path. Entries not starting with `route_` are legacy and ignored.

## Frontend architecture

- `views/HomeView.vue` hosts `components/Sidebar.vue` (planner, schedule board, routes list) and `components/MapView.vue` (the map and everything drawn on it). Sidebar emits `routeSelected` / `planSelected`; MapView receives `stations`, `selectedRouteId`, `tripPlan` props and exposes `centerMap`, `invalidateSize`, etc. via `defineExpose`.
- `MapView.vue` is very large and is being split up incrementally. Pure logic already extracted, with tests:
  - `utils/geo.ts` (haversine, bearing, nearest station, trimming a GTFS shape between two stops)
  - `services/osrmService.ts` (public OSRM for walking routes and road fallback) and `services/tripGeometry.ts` (bus segment = GTFS shape trimmed between stops, OSRM fallback)
  - `utils/liveBuses.ts` (parse Firebase data, choose visible buses, station ETAs) and `stores/liveBuses.ts` (one shared Firebase subscription, intended to become the SignalR source later; MapView is not switched to it yet).
- Live bus markers are **imperative Leaflet markers** in a layer group (not vue-leaflet components) so they can animate with a CSS transform transition; the icon is only rebuilt when its visual signature changes. Route casing (white outline) lives in a custom `casingPane` below the overlay pane, because vue-leaflet does not guarantee layer order.
- Map tiles are OpenStreetMap muted with a CSS filter (`.map-tiles`, inverted in dark mode). CARTO basemaps now require an API key, so they are not used.
- `services/apiService.ts` is the public API client. Its response interceptor runs `normalizeLineColorsDeep` (`utils/lineColors.ts`), which rewrites every `color`/`routeColor` hex to a same-hue variant with ≥4.5:1 contrast against white. The admin client (`services/adminService.ts`, which also holds `authService` and the JWT in localStorage) is deliberately not normalized. Cached color/route lists in localStorage use versioned keys (`routeColors.v2`, `routesList.v2`); bump the key if the cached shape or normalization changes.
- Router guards (`router/index.ts`) use `meta.requiresAuth` / `meta.requiresAdmin`; admin routes are lazy-loaded under `/admin`.
- i18n is a hand-rolled dictionary in `composables/useLanguage.ts`: `{ key: { ro, en } }` plus a separate `germanOverrides` map. `t(key, fallback = key, params)` returns the **key itself** when it is missing, so `t('x') || 'default'` never falls back; add the key to the dictionary (and to `germanOverrides`) instead. Duplicate keys break the type-check.
- Dark mode is a `.dark` class on the document (`composables/useDarkMode.ts`), not `prefers-color-scheme`; style dark variants with `.dark`.

## Design system ("Semnal")

The UI is being redesigned to the "Semnal" design (Claude Design canvas: https://claude.ai/artifact/JyzxXR7tRtmG66N9icBptv). Tokens are in `src/assets/main.css` (light + `.dark`): warm paper background, ink text, one cobalt accent (`--accent-primary`), amber `--live`/`--live-soft`/`--live-ink` reserved for live data, Archivo (`--font-display`) for headings, times and line numbers, Public Sans for text, tabular numbers. Old `--gradient-*` tokens resolve to flat colors on purpose.

Conventions used in redesigned components: use the global `.line-pill` (line number on its line color) and `.live-badge` classes; Lucide icons (`lucide-vue-next`) instead of emoji; real `<button>`s with `aria-label` for icon-only controls; 44 px minimum touch targets; shared stylesheets for repeated layouts (`assets/trip-panel.css` for trip detail panels, `assets/auth.css` for login/sign-up). Not yet redesigned: admin pages, tickets/checkout, station details, favorites, settings, statistics, trip history, walking directions, onboarding.

## Gotchas

- `vite-plugin-vue-devtools` adds a floating "V" pill at the bottom of the page in dev; it is not part of the app.
- After a Vite hot reload of `MapView.vue` the imperative bus layer can show 0 markers; reload the page before concluding markers are broken.
- On Render's free plan the simulator sleeps, so Firebase positions can be days old; check `bus_locations/<id>/timestamp` before debugging "frozen" buses.
- The bundled `TursibDb.db` contains real user accounts and is used by production; do not reset or commit changes to it casually.
