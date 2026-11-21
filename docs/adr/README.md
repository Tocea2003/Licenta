# Architecture Decision Records

Deciziile de arhitectură ale proiectului, în formatul lui Michael Nygard: context, decizie, consecințe.
Un ADR acceptat nu se rescrie; o decizie nouă îl înlocuiește și îl marchează „Înlocuit de …”.

| Nr. | Titlu | Stare |
|---|---|---|
| [001](001-mqtt-and-kafka-event-pipeline.md) | MQTT pentru dispozitive, Kafka (Redpanda) ca flux de evenimente | Acceptat |
| [002](002-signalr-with-firebase-fallback.md) | SignalR pentru harta live, Firebase ca rezervă | Acceptat |
| [003](003-sampled-position-history.md) | Istoric de poziții eșantionat în PostgreSQL | Acceptat |
| [004](004-embedded-ingest-for-free-tier.md) | Ingestie MQTT integrată în backend pentru planul gratuit | Acceptat |
| [005](005-trip-matching-for-gtfs-rt.md) | `trip_id` dedus din orar pentru GTFS-Realtime | Acceptat |
| [006](006-headway-on-round-trip-loop.md) | Headway măsurat pe bucla dus-întors, alerte cu histerezis | Acceptat |
| [007](007-digital-twin-model.md) | Digital twin: model agregat de pasageri, determinist | Acceptat |
| [008](008-holding-and-reinforcement-learning.md) | Holding clasic vs Q-learning tabular, comparate în twin | Acceptat |
| [009](009-opentelemetry.md) | Observabilitate cu OpenTelemetry | Acceptat |

O versiune anterioară a ADR-urilor 001–003 a fost scrisă pe ramura `stage2c-frontend`, pentru o implementare
paralelă a etapei 2. Cele de aici descriu implementarea care a rămas.
