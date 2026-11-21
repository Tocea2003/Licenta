# Cerințe și trasabilitate

Fiecare cerință are o **sursă** (din propunerea proiectului), o **implementare** (fișiere din repo) și o **verificare**
(teste automate sau demonstrații). `RequirementsTraceabilityTests` citește acest fișier și eșuează dacă un fișier sau un
test indicat nu mai există, deci trasabilitatea nu se poate desincroniza în tăcere.

Convenții:
- `F` = funcțională, `Q` = de calitate (non-funcțională).
- Implementare: căi relative la rădăcina repo-ului.
- Verificare: `Clasă` sau `Clasă.Metodă` din `TursibBackend.Tests`; fișierele de test din frontend sunt date ca
  `test:cale`; o demonstrație manuală e marcată `demo:`.

Propunerea, pe scurt: din prototipul monolitic de licență, un sistem distribuit pe evenimente. Dispozitive simulate
publică pozițiile pe MQTT, într-un flux Kafka, iar sistemul expune GTFS-Realtime. Un digital twin simulează autobuze și
pasageri pentru scenarii „what-if”. Obiectivul de cercetare e detecția și reducerea bus bunching-ului: headway-uri în
timp real și compararea holding-ului clasic cu reinforcement learning, după timpul de așteptare și varianța
headway-ului. Abordarea e de ingineria sistemelor: cerințe trasabile, model SysML/C4, decizii documentate,
observabilitate și teste de încărcare și de toleranță la erori.

## Funcționale

### REQ-F-01: Dispozitivele publică pozițiile pe MQTT
- **Cerință:** fiecare vehicul publică poziția periodic pe `tursib/vehicles/{vehicleId}/position`, cu TLS și
  autentificare când brokerul le cere.
- **Sursă:** propunere, „simulated on-board devices will publish vehicle positions over MQTT”.
- **Implementare:** `BusSimulator/Program.cs`, `TursibMessaging/MqttSubscriber.cs`, `infra/mosquitto/mosquitto.conf`
- **Verificare:** `MqttToKafkaIntegrationTests`, `SecureBrokerIntegrationTests.MqttSubscriber_ConnectsOverTlsWithCredentials_AndReceivesMessages`, demo: docker compose (docs/DEPLOYMENT.md §6)

### REQ-F-02: Validare la intrare și DLQ
- **Cerință:** mesajele invalide (JSON, dimensiune, topic, id falsificat, coordonate, timp) sunt respinse, numărate și
  păstrate cu motivul.
- **Sursă:** cerință derivată din REQ-F-01 (date de la dispozitive nesigure).
- **Implementare:** `TursibMessaging/VehiclePositionMessage.cs`, `TursibIngestion/IngestionPipeline.cs`, `TursibIngestion/KafkaPositionSink.cs`
- **Verificare:** `PositionParserTests`, `PositionIngestorTests.InvalidMessage_GoesToTheDeadLetterQueue_AndIsCounted`, `MqttToKafkaIntegrationTests.ValidMqttMessages_ReachKafka_AndInvalidOnesTheDeadLetterTopic`

### REQ-F-03: Flux de evenimente ordonat per vehicul
- **Cerință:** pozițiile validate intră în topicul Kafka `vehicle-positions`, cu cheia = id-ul vehiculului.
- **Sursă:** propunere, „into an event stream (Kafka)”.
- **Implementare:** `TursibIngestion/KafkaPositionSink.cs`, `docker-compose.yml`
- **Verificare:** `MqttToKafkaIntegrationTests.ValidMqttMessages_ReachKafka_AndInvalidOnesTheDeadLetterTopic`, `PositionIngestorTests.ValidMessage_IsPublishedKeyedByVehicle`

### REQ-F-04: Consumul fluxului de către platformă, cel puțin o dată
- **Cerință:** backend-ul consumă fluxul, aplică pozițiile și comite offset-ul după aplicare; reprocesarea nu strică
  starea.
- **Sursă:** cerință derivată din REQ-F-03.
- **Implementare:** `TursibBackend/Services/KafkaPositionConsumer.cs`, `TursibBackend/Services/LiveIngestService.cs`, `TursibBackend/Services/LiveBusService.cs`
- **Verificare:** `KafkaToBackendIntegrationTests`, `LiveIngestServiceTests.ReprocessingTheSameMessage_IsHarmless`, `LiveBusStateTests.Apply_IgnoresOlderPositionForSameBus`

### REQ-F-05: Harta live primește pozițiile de la platformă
- **Cerință:** clienții primesc un snapshot la conectare și apoi loturi de poziții, prin SignalR, cu Firebase ca rezervă.
- **Sursă:** continuarea aplicației de licență pe noua arhitectură.
- **Implementare:** `TursibBackend/Hubs/BusHub.cs`, `TursibFrontend/Frontend/src/stores/liveBuses.ts`
- **Verificare:** `LiveEndpointsTests.SignalR_ClientGetsSnapshotAndLiveBatches`, test:TursibFrontend/Frontend/src/utils/liveBuses.test.ts

### REQ-F-06: Feed GTFS-Realtime
- **Cerință:** sistemul expune `VehiclePositions` în formatul standard GTFS-Realtime (protobuf), cu numerele oficiale
  ale câmpurilor.
- **Sursă:** propunere, „the system will expose a standard GTFS-Realtime feed”.
- **Implementare:** `TursibBackend/Protos/gtfs-realtime.proto`, `TursibBackend/Services/GtfsRealtimeFeedBuilder.cs`, `TursibBackend/Controllers/GtfsRealtimeController.cs`
- **Verificare:** `GtfsRealtimeFeedBuilderTests`, `GtfsRealtimeEndpointTests`

### REQ-F-07: Cursa (trip_id) fiecărui vehicul
- **Cerință:** vehiculele din feed sunt legate de cursa programată cea mai plauzibilă, stabil și exclusiv.
- **Sursă:** cerință derivată din REQ-F-06 (consumatorii GTFS-RT leagă pozițiile de curse).
- **Implementare:** `TursibBackend/Services/TripMatcher.cs`
- **Verificare:** `TripMatcherTests`, `GtfsRealtimeTripEndpointTests`

### REQ-F-08: Istoric și replay
- **Cerință:** pozițiile sunt păstrate eșantionat și pot fi redate pe hartă de un administrator.
- **Sursă:** cerință derivată (analiza ulterioară a bunching-ului).
- **Implementare:** `TursibBackend/Services/PositionHistory.cs`, `TursibBackend/Controllers/LiveController.cs`, `TursibFrontend/Frontend/src/views/AdminReplay.vue`
- **Verificare:** `PositionHistoryTests`, `PositionHistoryEndpointTests`, test:TursibFrontend/Frontend/src/views/AdminReplay.test.ts

### REQ-F-09: Headway în timp real și alerte de bunching
- **Cerință:** golurile dintre autobuzele consecutive ale unei linii se măsoară continuu; sub un prag persistent apare
  o alertă, care se închide cu histerezis.
- **Sursă:** propunere, „I will measure the headways between consecutive buses in real time”.
- **Implementare:** `TursibBackend/Services/BunchingService.cs`, `TursibFrontend/Frontend/src/components/BunchingPanel.vue`
- **Verificare:** `RouteGeometryTests`, `BunchingAnalyzerTests`, `BunchingTrackerTests`, `BunchingEndToEndTests`

### REQ-F-10: Digital twin cu autobuze și pasageri
- **Cerință:** twin-ul simulează flota și pasagerii (sosiri în stații, urcare, coborâre, încărcătură) și măsoară
  timpul de așteptare.
- **Sursă:** propunere, „a digital twin of the TURSIB network that simulates both buses and passengers”.
- **Implementare:** `TursibBackend/Services/TwinEngine.cs`, `TursibBackend/Services/TwinService.cs`
- **Verificare:** `TwinEngineTests`, `TwinPassengerMetricsTests`, `TwinServiceTests`

### REQ-F-11: Scenarii what-if
- **Cerință:** twin-ul evaluează autobuze suplimentare, frecvențe schimbate și străzi închise, înainte de aplicare.
- **Sursă:** propunere, „what-if scenarios (extra buses, changed frequencies, closed streets)”.
- **Implementare:** `TursibBackend/Services/TwinEngine.cs`, `TursibBackend/Services/TwinService.cs`, `TursibFrontend/Frontend/src/components/TwinPanel.vue`
- **Verificare:** `TwinWhatIfTests`, `TwinWhatIfEndpointTests`, test:TursibFrontend/Frontend/src/utils/twin.test.ts

### REQ-F-12: Control clasic prin holding
- **Cerință:** o strategie de holding reține autobuzele prea apropiate de cel din față.
- **Sursă:** propunere, „a classical holding-control strategy”.
- **Implementare:** `TursibBackend/Services/HoldControl.cs`
- **Verificare:** `HoldPolicyTests`, `TwinStrategyTests`

### REQ-F-13: Control prin reinforcement learning
- **Cerință:** o strategie învățată prin reinforcement learning, antrenată în twin pe alte scenarii decât cele de test.
- **Sursă:** propunere, „compare a classical holding-control strategy with a reinforcement-learning one”.
- **Implementare:** `TursibBackend/Services/RlHoldControl.cs`, `TursibBackend/Resources/rl-hold-policy.json`
- **Verificare:** `RlHoldControlTests`

### REQ-F-14: Comparația strategiilor după așteptare și varianța headway-ului
- **Cerință:** strategiile se compară pe aceleași scenarii și seed-uri, după timpul de așteptare și varianța
  headway-ului, cu incertitudinea raportată.
- **Sursă:** propunere, „using waiting time and headway variance as metrics”.
- **Implementare:** `TursibBackend/Services/TwinExperiment.cs`, `docs/results/bunching-control.md`
- **Verificare:** `TwinExperimentTests`, `TwinPassengerMetricsTests.MeanWait_IsHalfTheMeanHeadwayPlusTheExcessWait`

### REQ-F-15: Recomandări de holding pentru dispecer
- **Cerință:** fiecare alertă activă vine cu reținerea recomandată pentru autobuzul din spate.
- **Sursă:** cerință derivată din REQ-F-09 și REQ-F-12.
- **Implementare:** `TursibBackend/Services/BunchingService.cs`, `TursibFrontend/Frontend/src/components/BunchingPanel.vue`
- **Verificare:** `HoldPolicyTests.Analyzer_AddsARecommendation_WhenHoldOptionsAreGiven`, `BunchingEndToEndTests.BunchedBuses_RaiseAlert_ViaRestAndSignalR`

## De calitate

### REQ-Q-01: Securitatea intrării
- **Cerință:** un dispozitiv nu poate raporta pentru alt vehicul; brokerele acceptă TLS și autentificare; ingestul
  HTTP cere o cheie.
- **Sursă:** cerință derivată (dispozitive în afara perimetrului).
- **Implementare:** `TursibMessaging/VehiclePositionMessage.cs`, `TursibMessaging/MqttSubscriber.cs`, `TursibMessaging/KafkaConnectionOptions.cs`, `TursibBackend/Controllers/LiveController.cs`
- **Verificare:** `PositionParserTests`, `SecureBrokerIntegrationTests`, `LiveEndpointsTests.Ingest_Returns401_WithoutOrWithWrongKey`

### REQ-Q-02: Acces la istoric doar pentru administratori
- **Implementare:** `TursibBackend/Controllers/LiveController.cs`
- **Cerință:** istoricul de poziții e disponibil doar rolului `Admin`.
- **Sursă:** cerință derivată (istoricul agregat e mai sensibil și mai costisitor decât poziția curentă).
- **Verificare:** `PositionHistoryEndpointTests.History_IsOnlyForAdministrators`

### REQ-Q-03: Toleranță la erori
- **Cerință:** căderea brokerului MQTT, a lui Kafka sau a backend-ului nu pierde pozițiile deja în flux și nu blochează
  componentele; acestea își revin singure când serviciul revine.
- **Sursă:** propunere, „fault-tolerance testing”.
- **Implementare:** `TursibMessaging/MqttSubscriber.cs`, `TursibBackend/Services/KafkaPositionConsumer.cs`, `TursibBackend/Services/PositionHistory.cs`, `tools/resilience/run-fault-tests.sh`
- **Verificare:** `FaultToleranceTests`, demo: tools/resilience/run-fault-tests.sh (rezultate în docs/results/fault-tolerance.md)

### REQ-Q-04: Observabilitate
- **Cerință:** drumul unei poziții (MQTT → ingestie → Kafka → backend) e urmărit cu OpenTelemetry (urme și metrici,
  exportate prin OTLP).
- **Sursă:** propunere, „observability (OpenTelemetry)”.
- **Implementare:** `TursibMessaging/Telemetry.cs`, `TursibBackend/Program.cs`, `TursibIngestion/Program.cs`, `infra/otel/otel-collector.yaml`
- **Verificare:** `TelemetryTests`

### REQ-Q-05: Performanță sub încărcare
- **Cerință:** platforma ingerează și distribuie pozițiile unei flote de mărimea celei Tursib (~170 de autobuze la
  0,5 s) cu latență mică, iar limitele sunt măsurate.
- **Sursă:** propunere, „load … testing”.
- **Implementare:** `tools/loadtest/Program.cs`
- **Verificare:** `LoadTestHarnessTests`, demo: tools/loadtest (rezultate în docs/results/load-test.md)

### REQ-Q-06: Reproductibilitatea experimentelor
- **Cerință:** o rulare a twin-ului e deterministă pentru un seed; seed-urile de antrenare, validare și test sunt disjuncte.
- **Sursă:** cerință derivată din REQ-F-14.
- **Implementare:** `TursibBackend/Services/TwinEngine.cs`, `TursibBackend/Services/RlHoldControl.cs`
- **Verificare:** `TwinEngineTests.SameSeed_GivesIdenticalResults`, `RlHoldControlTests.TrainingIsDeterministicForASeed`

### REQ-Q-07: Implementare în cloud gratuit și verificabilă
- **Cerință:** sistemul rulează pe planuri gratuite (Render, Supabase, broker MQTT gestionat), iar starea se poate
  verifica dintr-un endpoint.
- **Sursă:** constrângere de proiect.
- **Implementare:** `render.yaml`, `docs/DEPLOYMENT.md`, `TursibBackend/Services/EmbeddedMqttIngest.cs`, `TursibBackend/Controllers/StatusController.cs`
- **Verificare:** `StatusEndpointTests`, `EmbeddedMqttIngestTests`, `EmbeddedMqttIntegrationTests`

### REQ-Q-08: Volum de date controlat
- **Cerință:** istoricul crește previzibil (eșantionare, tampon mărginit, retenție).
- **Sursă:** constrângere (planul gratuit Supabase, 500 MB).
- **Implementare:** `TursibBackend/Services/PositionHistory.cs`
- **Verificare:** `PositionHistoryTests.Buffer_KeepsOnePositionPerBusPerInterval`, `PositionHistoryTests.Buffer_DropsTheOldestWhenFull`, `PositionHistoryTests.Writer_FlushesTheBufferToTheDatabase_AndPurgesOldRows`
