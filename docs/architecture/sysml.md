# Modelul SysML

Modelul textual e în [`tursib.sysml`](tursib.sysml) (SysML v2): blocuri (`part def`), porturi și elementele care
circulă prin ele, conexiuni, comportamentul principal, mașina de stări a alertei și cerințele, cu legături `satisfy`
către părțile care le îndeplinesc. Mai jos, diagramele principale, redate în Mermaid.

## Definiția blocurilor (BDD)

```mermaid
classDiagram
  class TursibTracker
  TursibTracker *-- "1..*" OnBoardDevice
  TursibTracker *-- MqttBroker
  TursibTracker *-- IngestionService
  TursibTracker *-- EventStream
  TursibTracker *-- LivePlatform
  TursibTracker *-- DigitalTwin
  TursibTracker *-- WebApp
  TursibTracker *-- Database
  TursibTracker *-- TelemetryCollector
  LivePlatform *-- LiveState
  LivePlatform *-- BunchingDetector
  LivePlatform *-- PositionHistory
  LivePlatform *-- TripMatcher
  DigitalTwin *-- TwinEngine
  DigitalTwin *-- WhatIf
  DigitalTwin o-- ControlStrategy
  ControlStrategy <|-- HoldingControl
  ControlStrategy <|-- ReinforcementLearningControl
  class VehiclePosition {
    id
    routeId
    latitude
    longitude
    timestamp
    speedKmh
    directionId
    nextStationId
  }
```

## Blocul intern (IBD): drumul unei poziții

```mermaid
flowchart LR
  D["devices : OnBoardDevice[1..*]"] -- "mqtt : VehiclePosition" --> M["broker : MqttBroker"]
  M -- "VehiclePosition" --> I["ingestion : IngestionService"]
  I -- "outbound : VehiclePosition" --> S["stream : EventStream"]
  I -- "deadLetter" --> DLQ[("vehicle-positions.dlq")]
  S --> P["platform : LivePlatform"]
  P -- "live : positions, alerts" --> A["app : WebApp"]
  P -- "gtfsRt" --> X(("consumatori GTFS-RT"))
```

## Activitate: procesarea unei poziții

```mermaid
flowchart TD
  start((•)) --> r[Primește mesaj MQTT]
  r --> v{Valid?}
  v -- nu --> dlq[Scrie în DLQ cu motivul] --> stop((◉))
  v -- da --> k[Scrie pe vehicle-positions, cheie = vehicul]
  k --> c[Consumator: lot de până la 500 mesaje / 100 ms]
  c --> n{Mai nouă decât poziția cunoscută?}
  n -- nu --> skip[Ignoră] --> commit
  n -- da --> st[Actualizează starea live] --> pub[Publică pe magistrala internă]
  pub --> h1[SignalR → hartă]
  pub --> h2[Detector bunching]
  pub --> h3[Istoric eșantionat]
  h1 & h2 & h3 --> commit[Commit offset] --> stop
```

## Stări: alerta de bunching

```mermaid
stateDiagram-v2
  [*] --> Normal
  Normal --> Pending: raport < 0,35
  Pending --> Normal: raport ≥ 0,35
  Pending --> Active: persistă 10 s
  Active --> Active: raport între 0,35 și 0,6 (histerezis)
  Active --> Normal: raport > 0,6 sau pereche dispărută
```

## Secvență: de la dispozitiv la hartă

```mermaid
sequenceDiagram
  participant Dev as Dispozitiv
  participant MQ as Broker MQTT
  participant Ing as Ingestie
  participant K as Kafka
  participant BE as Backend
  participant FE as Harta
  Dev->>MQ: PUBLISH tursib/vehicles/route_5_bus_0/position
  MQ->>Ing: mesaj
  Ing->>Ing: PositionParser (id din topic, coordonate, timp)
  Ing->>K: produce(key=route_5_bus_0)
  K->>BE: lot (consumer group tursib-backend)
  BE->>BE: LiveIngestService.Ingest → LiveBusState
  BE-->>FE: SignalR "positions"
  BE->>K: commit offset
```
