/*
 * Modelul C4 al sistemului TURSIB (Structurizr DSL). Vizualizare: https://structurizr.com/dsl
 * sau `docker run -it --rm -p 8080:8080 -v $PWD:/usr/local/structurizr structurizr/lite`.
 * Variantă Mermaid, afișată direct pe GitHub: c4.md.
 */
workspace "TURSIB Tracker" "Urmărire în timp real, digital twin și controlul bus bunching pentru transportul public din Sibiu." {

    model {
        passenger = person "Călător" "Vede autobuzele pe hartă, planifică trasee, cumpără bilete."
        dispatcher = person "Dispecer / administrator" "Urmărește alertele de bunching, rulează scenarii în twin, redă istoricul."
        device = softwareSystem "Dispozitiv de bord" "AVL din autobuz (aici: BusSimulator). Publică poziția periodic." "External"
        gtfs = softwareSystem "Feed GTFS Tursib" "Orarul oficial (tursib.ro)." "External"
        consumers = softwareSystem "Consumatori GTFS-RT" "Google Maps, Transit, OneBusAway." "External"
        osm = softwareSystem "OSM / OSRM / Valhalla" "Hărți și rute pietonale." "External"

        tursib = softwareSystem "TURSIB Tracker" {
            frontend = container "Aplicație web / mobilă" "Hartă live, planificator, bilete, admin (replay, bunching, twin)." "Vue 3, TypeScript, PWA, Capacitor" "Web"
            broker = container "Broker MQTT" "Primește pozițiile dispozitivelor pe tursib/vehicles/{id}/position." "Mosquitto / HiveMQ Cloud" "Queue"
            ingestion = container "Serviciu de ingestie" "Validează mesajele, scrie pe Kafka, respinsele în DLQ." ".NET 9 worker"
            stream = container "Flux de evenimente" "Topicurile vehicle-positions (cheie = vehicul) și vehicle-positions.dlq." "Kafka / Redpanda" "Queue"
            backend = container "API + platformă live" "REST, SignalR, GTFS-RT, bunching, twin, control, istoric." "ASP.NET Core 9" {
                consumer = component "KafkaPositionConsumer / EmbeddedMqttIngest" "Citește pozițiile (Kafka sau MQTT direct)."
                ingest = component "LiveIngestService" "Punct unic de intrare: validare, stare live, publicare pe magistrală."
                state = component "LiveBusState" "Ultima poziție per autobuz."
                bus = component "LiveEventBus + Dispatcher" "Magistrală internă; livrează loturile tuturor IBusPositionHandler."
                hub = component "BusHub (SignalR)" "Snapshot și loturi către hartă; alerte de bunching."
                bunching = component "BunchingDetector" "Proiectare pe formă, headway pe bucla dus-întors, alerte cu histerezis, recomandări de holding."
                history = component "PositionHistory" "Eșantionare, tampon, scriere în loturi, retenție."
                gtfsrt = component "GTFS-RT + TripMatcher" "Feed VehiclePositions; trip_id dedus din orar."
                twin = component "Digital twin" "TwinEngine, what-if, TwinRunner, TwinExperiment."
                control = component "Control bunching" "HoldPolicy (headway, equalize), RlHoldControl (Q-learning)."
                planner = component "RouteCalculatorService" "Planificator pe orar real, cu fallback estimat."
            }
            db = container "Bază de date" "GTFS, utilizatori, bilete, istoric de poziții." "PostgreSQL (Supabase)" "Database"
            telemetry = container "Colector OpenTelemetry" "Urme și metrici de la backend și ingestie." "OTel Collector + Jaeger / Prometheus" "Tooling"
        }

        device -> broker "Publică poziții" "MQTT, TLS"
        broker -> ingestion "Livrează mesaje" "MQTT"
        ingestion -> stream "Scrie poziții validate / respinse" "Kafka"
        stream -> consumer "Consumă vehicle-positions" "Kafka"
        broker -> consumer "Ingestie integrată (plan gratuit)" "MQTT, TLS"
        consumer -> ingest "Loturi de poziții"
        ingest -> state "Aplică"
        ingest -> bus "Publică"
        bus -> hub "Livrează"
        bus -> bunching "Livrează"
        bus -> history "Livrează"
        history -> db "Inserează în loturi" "EF Core"
        gtfsrt -> state "Citește"
        gtfsrt -> db "Orarul zilei" "EF Core"
        twin -> control "Rulează strategiile"
        bunching -> control "Recomandă holding"
        twin -> state "Pornește din pozițiile live"
        planner -> db "Curse, calendar" "EF Core"
        hub -> frontend "positions, bunching" "WebSocket (SignalR)"
        frontend -> backend "REST" "HTTPS/JSON"
        passenger -> frontend "Folosește"
        dispatcher -> frontend "Folosește (admin)"
        consumers -> gtfsrt "Citesc /api/gtfs-rt/vehicle-positions" "HTTPS, protobuf"
        gtfs -> db "Import lunar (GitHub Actions)" "ZIP → CSV"
        frontend -> osm "Tile-uri, rute pietonale" "HTTPS"
        backend -> telemetry "Urme, metrici" "OTLP"
        ingestion -> telemetry "Urme, metrici" "OTLP"
    }

    views {
        systemContext tursib "Context" {
            include *
            autolayout lr
        }
        container tursib "Containere" {
            include *
            autolayout lr
        }
        component backend "Componente-backend" {
            include *
            autolayout lr
        }
        styles {
            element "Person" { shape Person; background #08427b; color #ffffff }
            element "External" { background #999999; color #ffffff }
            element "Database" { shape Cylinder }
            element "Queue" { shape Pipe }
            element "Web" { shape WebBrowser }
        }
    }
}
