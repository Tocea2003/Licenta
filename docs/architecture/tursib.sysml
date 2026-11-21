// Modelul SysML v2 (notație textuală) al sistemului TURSIB Tracker.
// Structura (part def / port def), interfețele, comportamentul principal și cerințele, cu legăturile `satisfy`
// către componentele care le îndeplinesc. Identificatorii cerințelor sunt cei din docs/requirements.md.
// Se poate deschide în SysML v2 Pilot Implementation (Jupyter / Eclipse) sau în orice editor SysML v2 textual.

package TursibTracker {
    private import ScalarValues::*;
    private import ISQ::*;

    // ---------- Mesaje și porturi ----------
    attribute def VehiclePosition {
        attribute id : String;
        attribute routeId : Integer;
        attribute latitude : Real;
        attribute longitude : Real;
        attribute timestamp : Integer;
        attribute speedKmh : Real;
        attribute directionId : Integer;
        attribute nextStationId : Integer;
    }
    attribute def BunchingAlert {
        attribute followerId : String;
        attribute leaderId : String;
        attribute gapRatio : Real;
        attribute recommendedHoldSeconds : Real;
    }

    port def MqttPositionPort { out item position : VehiclePosition; }
    port def KafkaPositionPort { out item position : VehiclePosition; }
    port def LivePort { out item positions : VehiclePosition[*]; out item alerts : BunchingAlert[*]; }
    port def GtfsRtPort { out item feed; }

    // ---------- Structură (blocuri) ----------
    part def OnBoardDevice { port mqtt : MqttPositionPort; }
    part def MqttBroker { port inbound : ~MqttPositionPort; port outbound : MqttPositionPort; }
    part def IngestionService {
        port inbound : ~MqttPositionPort;
        port outbound : KafkaPositionPort;
        port deadLetter : KafkaPositionPort;
    }
    part def EventStream { port inbound : ~KafkaPositionPort; port outbound : KafkaPositionPort; }
    part def LivePlatform {
        port inbound : ~KafkaPositionPort;
        port live : LivePort;
        port gtfsRt : GtfsRtPort;
        part liveState;
        part bunchingDetector;
        part positionHistory;
        part tripMatcher;
    }
    part def DigitalTwin {
        part engine;
        part whatIf;
        part controlStrategies;
    }
    part def ControlStrategy;
    part def HoldingControl :> ControlStrategy;
    part def ReinforcementLearningControl :> ControlStrategy;
    part def WebApp { port live : ~LivePort; }
    part def Database;
    part def TelemetryCollector;

    part tursib {
        part devices : OnBoardDevice[1..*];
        part broker : MqttBroker;
        part ingestion : IngestionService;
        part stream : EventStream;
        part platform : LivePlatform;
        part twin : DigitalTwin;
        part holding : HoldingControl;
        part rl : ReinforcementLearningControl;
        part app : WebApp;
        part db : Database;
        part telemetry : TelemetryCollector;

        connect devices.mqtt to broker.inbound;
        connect broker.outbound to ingestion.inbound;
        connect ingestion.outbound to stream.inbound;
        connect stream.outbound to platform.inbound;
        connect platform.live to app.live;
    }

    // ---------- Comportament: drumul unei poziții ----------
    action def ProcessPosition {
        in item raw;
        action validate;
        action publishToStream;
        action applyToLiveState;
        action dispatchToHandlers;
        first validate then publishToStream;
        first publishToStream then applyToLiveState;
        first applyToLiveState then dispatchToHandlers;
    }

    // ---------- Stările unei alerte de bunching (histerezis) ----------
    state def BunchingAlertLifecycle {
        entry; then Normal;
        state Normal;
        state Pending;
        state Active;
        transition Normal_to_Pending first Normal accept when gapRatio < 0.35 then Pending;
        transition Pending_to_Normal first Pending accept when gapRatio >= 0.35 then Normal;
        transition Pending_to_Active first Pending accept after 10 [SI::s] then Active;
        transition Active_to_Normal first Active accept when gapRatio > 0.6 then Normal;
        attribute gapRatio : Real;
    }

    // ---------- Cerințe (rezumat; textul complet în docs/requirements.md) ----------
    requirement def <'REQ-F-01'> DevicesPublishOverMqtt { doc /* Dispozitivele publică pozițiile pe MQTT. */ }
    requirement def <'REQ-F-02'> ValidateAndDeadLetter { doc /* Mesajele invalide sunt respinse și păstrate în DLQ, cu motivul. */ }
    requirement def <'REQ-F-03'> OrderedEventStream { doc /* Pozițiile validate intră într-un flux Kafka, ordonate per vehicul. */ }
    requirement def <'REQ-F-06'> GtfsRealtimeFeed { doc /* Sistemul expune feed-ul standard GTFS-Realtime VehiclePositions. */ }
    requirement def <'REQ-F-09'> RealTimeHeadway { doc /* Headway-ul dintre autobuzele consecutive se măsoară în timp real; bunching-ul generează alerte. */ }
    requirement def <'REQ-F-10'> DigitalTwinBusesAndPassengers { doc /* Twin-ul simulează autobuze și pasageri. */ }
    requirement def <'REQ-F-11'> WhatIfScenarios { doc /* Twin-ul evaluează autobuze în plus, frecvențe schimbate și străzi închise. */ }
    requirement def <'REQ-F-12'> HoldingControl { doc /* O strategie clasică de holding reduce bunching-ul. */ }
    requirement def <'REQ-F-13'> RlControl { doc /* O strategie de reinforcement learning, antrenată în twin. */ }
    requirement def <'REQ-F-14'> CompareOnWaitAndVariance { doc /* Strategiile se compară după timpul de așteptare și varianța headway-ului. */ }
    requirement def <'REQ-Q-04'> Observability { doc /* Urme și metrici OpenTelemetry pe drumul unei poziții. */ }

    satisfy DevicesPublishOverMqtt by tursib.devices;
    satisfy ValidateAndDeadLetter by tursib.ingestion;
    satisfy OrderedEventStream by tursib.stream;
    satisfy GtfsRealtimeFeed by tursib.platform;
    satisfy RealTimeHeadway by tursib.platform.bunchingDetector;
    satisfy DigitalTwinBusesAndPassengers by tursib.twin;
    satisfy WhatIfScenarios by tursib.twin.whatIf;
    satisfy HoldingControl by tursib.holding;
    satisfy RlControl by tursib.rl;
    satisfy CompareOnWaitAndVariance by tursib.twin;
    satisfy Observability by tursib.telemetry;
}
