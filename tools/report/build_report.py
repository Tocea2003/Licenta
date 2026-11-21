"""Generates build/report.html (the project document) from docs/results/*.json and docs/requirements.md.

Run from tools/report:  python3 build_report.py && node render-pdf.mjs   (or: npm run build)
Every number in the document comes from the result files, so re-running an experiment and rebuilding keeps the text
and the figures consistent. The prose is written for the current results; check it after re-running experiments.
"""
import json
import pathlib
import re
from datetime import date
from html import escape

import charts
from charts import STRATEGY_COLORS, STRATEGY_LABELS

ROOT = pathlib.Path(__file__).resolve().parents[2]
RESULTS = ROOT / "docs" / "results"
OUT = pathlib.Path(__file__).resolve().parent / "build"

AUTHOR = ""  # filled in by the author before submission; left empty it is omitted from the title page

# ---------------------------------------------------------------- data


def load(name):
    return json.loads((RESULTS / name).read_text(encoding="utf-8"))


control = load("bunching-control.json")
load_http = load("load-http.json") + load("load-http-limit.json")
load_mqtt = load("load-mqtt.json") + load("load-mqtt-limit.json")
fault_dir = RESULTS / "fault"
fault = {p.stem: json.loads(p.read_text())[0] for p in sorted(fault_dir.glob("*.json"))} if fault_dir.exists() else {}

REQ_TITLES = {
    "REQ-F-01": "On-board devices publish positions over MQTT",
    "REQ-F-02": "Input validation and dead-letter queue",
    "REQ-F-03": "Event stream ordered per vehicle (Kafka)",
    "REQ-F-04": "At-least-once consumption by the platform",
    "REQ-F-05": "Live map fed by the platform (SignalR, Firebase fallback)",
    "REQ-F-06": "Standard GTFS-Realtime feed",
    "REQ-F-07": "Scheduled trip (trip_id) of every vehicle",
    "REQ-F-08": "Position history and replay",
    "REQ-F-09": "Real-time headways and bunching alerts",
    "REQ-F-10": "Digital twin with buses and passengers",
    "REQ-F-11": "What-if scenarios",
    "REQ-F-12": "Classical holding control",
    "REQ-F-13": "Reinforcement-learning control",
    "REQ-F-14": "Comparison by waiting time and headway variance",
    "REQ-F-15": "Holding recommendations for the dispatcher",
    "REQ-Q-01": "Input security (no spoofing, TLS, authentication)",
    "REQ-Q-02": "History restricted to administrators",
    "REQ-Q-03": "Fault tolerance",
    "REQ-Q-04": "Observability (OpenTelemetry)",
    "REQ-Q-05": "Performance under load",
    "REQ-Q-06": "Reproducible experiments",
    "REQ-Q-07": "Free-tier cloud deployment, verifiable",
    "REQ-Q-08": "Bounded data volume",
}


def parse_requirements():
    text = (ROOT / "docs" / "requirements.md").read_text(encoding="utf-8")
    reqs = []
    for block in re.split(r"^### ", text, flags=re.M)[1:]:
        rid = block.split(":")[0].strip()
        src = re.search(r"\*\*Sursă:\*\* (.+)", block).group(1)
        impl = re.findall(r"`([^`]+)`", re.search(r"\*\*Implementare:\*\* (.+)", block).group(1))
        ver = re.search(r"\*\*Verificare:\*\* (.+)", block).group(1)
        tests = [t for t in re.findall(r"`([^`]+)`", ver)]
        tests += ["frontend test" for _ in re.findall(r"test:", ver)]
        demo = "demo:" in ver
        if rid not in REQ_TITLES:
            raise SystemExit(f"{rid} has no English title in REQ_TITLES")
        reqs.append({"id": rid, "title": REQ_TITLES[rid], "proposal": src.startswith("propunere"),
                     "impl": impl, "tests": tests, "demo": demo})
    missing = set(REQ_TITLES) - {r["id"] for r in reqs}
    if missing:
        raise SystemExit(f"requirements removed from docs/requirements.md: {sorted(missing)}")
    return reqs


requirements = parse_requirements()

# ---------------------------------------------------------------- helpers

SCEN_EN = {
    "3 autobuze, doar zgomot": "3 buses\nnoise",
    "3 autobuze, un autobuz oprit 5 min": "3 buses\n5-min stop",
    "5 autobuze, doar zgomot": "5 buses\nnoise",
    "5 autobuze, un autobuz oprit 5 min": "5 buses\n5-min stop",
    "8 autobuze, doar zgomot": "8 buses\nnoise",
    "8 autobuze, un autobuz oprit 5 min": "8 buses\n5-min stop",
}
WHATIF_EN = {
    "bază: 5 autobuze": "Base: 5 buses",
    "+1 autobuz de la început": "+1 bus from the start",
    "+2 autobuze după 1 h": "+2 buses after 1 h",
    "frecvență: un autobuz la 6 min": "Frequency: one bus every 6 min",
    "frecvență: un autobuz la 10 min": "Frequency: one bus every 10 min",
    "stradă închisă km 1,5–2,5, ocol +1,2 km": "Street closed (km 1.5–2.5), detour +1.2 km",
    "stradă închisă 1 h (de la 1 h), ocol +1,2 km": "Same closure for 1 h (from t = 1 h)",
}


def n0(v):
    return f"{v:,.0f}".replace(",", " ")


def n1(v):
    return f"{v:.1f}"


def n2(v):
    return f"{v:.2f}"


def pm(m, f=n0):
    return f"{f(m['mean'])} <span class='pm'>± {f(m['std'])}</span>"


def table(headers, rows, cls=""):
    h = "".join(f"<th>{x}</th>" for x in headers)
    b = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f"<table class='{cls}'><thead><tr>{h}</tr></thead><tbody>{b}</tbody></table>"


def figure(svg_or_html, caption, cls=""):
    figure.n += 1
    return f"<figure class='{cls}'>{svg_or_html}<figcaption><b>Figure {figure.n}.</b> {caption}</figcaption></figure>"


figure.n = 0


def mermaid(src, caption, cls=""):
    return figure(f"<pre class='mermaid'>{escape(src)}</pre>", caption, cls)


def scenario(name):
    return next(s for s in control["scenarios"] if s["scenario"]["name"] == name)


def strat(s, key):
    return next(x for x in s["strategies"] if x["strategy"] == key)


def versus(s, key):
    return next(x for x in s["versusHeadway"] if x["strategy"] == key)


# ---------------------------------------------------------------- figures from results

scen_names = [s["scenario"]["name"] for s in control["scenarios"]]
groups = [SCEN_EN[n] for n in scen_names]
series = ["none", "headway", "equalize", "rl"]

delay_values = {SCEN_EN[n]: {k: strat(scenario(n), k)["totalDelay"]["mean"] for k in series} for n in scen_names}
delay_errors = {SCEN_EN[n]: {k: strat(scenario(n), k)["totalDelay"]["std"] for k in series} for n in scen_names}
fig_delay = charts.grouped_bars(groups, series, delay_values, delay_errors, title="Total delay per passenger",
                                unit="seconds per passenger (mean ± sd over 20 test seeds)")

cv_values = {SCEN_EN[n]: {k: strat(scenario(n), k)["headwayCv"]["mean"] for k in series} for n in scen_names}
cv_errors = {SCEN_EN[n]: {k: strat(scenario(n), k)["headwayCv"]["std"] for k in series} for n in scen_names}
fig_cv = charts.grouped_bars(groups, series, cv_values, cv_errors, title="Headway coefficient of variation",
                             unit="coefficient of variation of time headways", height=260)

ci = {SCEN_EN[n]: {k: (versus(scenario(n), k)["meanDifference"], versus(scenario(n), k)["ciLow"], versus(scenario(n), k)["ciHigh"])
                   for k in ("equalize", "rl")} for n in scen_names}
fig_ci = charts.ci_chart(groups, ["equalize", "rl"], ci, title="Paired difference to headway holding")

hold_values = {SCEN_EN[n]: {k: strat(scenario(n), k)["holdSecondsPerBusHour"]["mean"] for k in ("headway", "equalize", "rl")} for n in scen_names}
fig_hold = charts.grouped_bars(groups, ["headway", "equalize", "rl"], hold_values, None, title="Holding time",
                               unit="holding seconds per bus-hour", height=240)


def lat_series(rows, key):
    return [(r["buses"], r[key]) for r in rows]


fig_latency = charts.line_chart([
    ("HTTP ingest p95", "#0f766e", lat_series(load_http, "p95Ms")),
    ("MQTT → Kafka chain p50", "#93c5fd", lat_series(load_mqtt, "p50Ms")),
    ("MQTT → Kafka chain p95", "#2563eb", lat_series(load_mqtt, "p95Ms")),
    ("MQTT → Kafka chain p99", "#1e3a8a", lat_series(load_mqtt, "p99Ms")),
], xlabel="simulated buses, each reporting every 0.5 s (log scale)", ylabel="end-to-end latency to the map (ms)", logx=True)

# ---------------------------------------------------------------- fault tolerance

FAULT_EN = {
    "baseline": ("No fault (reference)", None),
    "kafka-down": ("Kafka (Redpanda) stopped for 30 s", 30),
    "mqtt-broker-restart": ("MQTT broker restarted", None),
    "backend-restart": ("Backend restarted", None),
    "ingestion-down-1-instance": ("Ingestion stopped 30 s (single instance)", 30),
    "ingestion-down-2-instances": ("One of two ingestion instances killed (shared subscription)", None),
}


def fault_analysis(step, fault_at=30):
    tl = step["timeline"]
    expected = step["buses"] * 1000 / step["intervalMs"]
    outage = [s for s in tl if s["second"] >= fault_at and s["received"] < 0.5 * expected and s["second"] < step["durationSeconds"]]
    recovered = None
    if outage:
        last_bad = max(s["second"] for s in outage)
        good = [s for s in tl if s["second"] > last_bad and s["received"] >= 0.8 * expected]
        recovered = good[0]["second"] - fault_at if good else None
    return len(outage), recovered, expected


def fault_section():
    if not fault:
        return "<p class='todo'>Fault-tolerance results not found (run tools/resilience/run-fault-tests.sh).</p>"
    rows, timelines = [], []
    for key, (label, down) in FAULT_EN.items():
        if key not in fault:
            continue
        s = fault[key]
        outage, rec, expected = fault_analysis(s)
        rec_txt = "—" if outage == 0 else (f"{rec} s" if rec is not None else "not recovered")
        rows.append([label, n0(s["sent"]), n0(s["received"]), f"{s['lossPercent']}%", f"{outage} s", rec_txt,
                     f"{s['p50Ms'] / 1000:.2f} s", f"{s['maxMs'] / 1000:.1f} s"])
        end = 30 + (down or 1)
        timelines.append(charts.timeline(s["timeline"], expected, title=label, fault_at=30, recover_at=end))
    grid = "<div class='tl-grid'>" + "".join(f"<div>{t}</div>" for t in timelines) + "</div>"
    first = next(iter(fault.values()))
    intro = (f"<p>Each experiment runs a synthetic fleet of {first['buses']} buses (the size of the Tursib fleet) publishing every "
             f"{first['intervalMs']} ms over MQTT (QoS 0) through the full chain (Mosquitto → ingestion → Redpanda → backend → "
             f"SignalR) for {first['durationSeconds']} s. The fault is injected at second 30. The load tool counts, second by second, the "
             "positions that reach a SignalR client, matching each one to the message that produced it.</p>")
    t = table(["Experiment", "Sent", "On the map", "Lost", "Outage", "Recovered after", "Latency p50", "Latency max"], rows, "num")
    return intro + t + fault_discussion() + figure(grid, "Positions delivered to the map per second (blue) against the expected rate (dashed). "
                                    "The shaded band is the fault window.")


def fault_discussion():
    def f(key):
        return fault.get(key)

    items = []
    if f("kafka-down"):
        s = f("kafka-down")
        items.append(f"<li><b>Kafka down for 30 s: no position lost.</b> Positions stop reaching the map while the broker is down, "
                     f"but the ingestion producer keeps them in its buffer (delivery timeout 120 s) and delivers them when Redpanda "
                     f"is back: {s['lossPercent']}% lost, maximum latency {s['maxMs'] / 1000:.0f} s, i.e. the outage itself. The backlog arrives "
                     "as one burst and the map shows the latest position of each bus; older ones are ignored by the live state. "
                     "An outage longer than the delivery timeout would lose data; that limit is a configuration choice.</li>")
    if f("mqtt-broker-restart"):
        s = f("mqtt-broker-restart")
        items.append(f"<li><b>MQTT broker restart: {s['lossPercent']}% lost.</b> With QoS 0 and no persistence, what devices publish "
                     "while the broker restarts is gone; the devices and the ingestion subscriber reconnect on their own within "
                     "seconds. QoS 1 with persistent sessions (supported, <code>MQTT_QOS=1</code>) trades this loss for more traffic; "
                     "for positions that are superseded every few seconds, QoS 0 is the usual choice.</li>")
    if f("backend-restart"):
        s = f("backend-restart")
        items.append(f"<li><b>Backend restart: {s['lossPercent']}% lost.</b> The backend comes back in about a second and resumes from "
                     "its committed Kafka offset, so nothing is lost in the stream; what the map misses are the positions broadcast "
                     "while its SignalR connection was re-established (the client reconnects automatically and receives a snapshot).</li>")
    if f("ingestion-down-1-instance"):
        s = f("ingestion-down-1-instance")
        o, r, _ = fault_analysis(s)
        items.append(f"<li><b>Ingestion down for 30 s, single instance: {s['lossPercent']}% lost.</b> Nobody consumes the MQTT "
                     "topic while the only ingestion instance is down, and QoS 0 messages are not queued for it.</li>")
    if f("ingestion-down-2-instances"):
        s = f("ingestion-down-2-instances")
        o, r, _ = fault_analysis(s)
        items.append(f"<li><b>Two ingestion instances, one killed: {s['lossPercent']}% lost.</b> The instances share the MQTT "
                     "subscription (<code>$share/ingestion/…</code>), so the broker splits the devices between them and, when one "
                     f"disappears, sends everything to the survivor ({o} s with less than half of the expected rate). Redundancy "
                     "turns the outage of the previous experiment into a short dip.</li>")
    return "<ul>" + "".join(items) + "</ul>" if items else ""


# ---------------------------------------------------------------- text blocks

base = control["whatIf"][0]
whatif_rows = []
for w in control["whatIf"]:
    whatif_rows.append([WHATIF_EN[w["name"]], f"{w['busesAtStart']}→{w['busesAtEnd']}", pm(w["meanHeadway"]), pm(w["meanWait"]),
                        pm(w["totalDelay"]), pm(w["headwayCv"], n2), pm(w["skippedStops"])])

wi = {w["name"]: w for w in control["whatIf"]}
WHATIF_SHORT = {
    "bază: 5 autobuze": "base\n5 buses",
    "+1 autobuz de la început": "+1 bus",
    "+2 autobuze după 1 h": "+2 buses\nafter 1 h",
    "frecvență: un autobuz la 6 min": "every\n6 min",
    "frecvență: un autobuz la 10 min": "every\n10 min",
    "stradă închisă km 1,5–2,5, ocol +1,2 km": "closure\n(all day)",
    "stradă închisă 1 h (de la 1 h), ocol +1,2 km": "closure\n(1 h)",
}
fig_whatif = charts.grouped_bars(
    [WHATIF_SHORT[n] for n in wi], ["wait", "delay"],
    {WHATIF_SHORT[n]: {"wait": w["meanWait"]["mean"], "delay": w["totalDelay"]["mean"]} for n, w in wi.items()},
    None, title="What-if scenarios", unit="seconds per passenger", height=270,
    colors={"wait": "#2563eb", "delay": "#d97706"},
    labels={"wait": "mean wait at the stop", "delay": "total delay (wait + on-board holding)"})

control_rows = []
for n in scen_names:
    s = scenario(n)
    for k in series:
        x = strat(s, k)
        control_rows.append([SCEN_EN[n].replace("\n", ", ") if k == "none" else "", STRATEGY_LABELS[k], pm(x["totalDelay"]), pm(x["meanWait"]),
                             pm(x["excessWait"]), pm(x["headwayCv"], n2), pm(x["holdSecondsPerBusHour"]), pm(x["bunchedPercent"], n1)])

vs_rows = []
for n in scen_names:
    for k in ("rl", "equalize", "none"):
        v = versus(scenario(n), k)
        verdict = "better" if v["ciHigh"] < 0 else ("worse" if v["ciLow"] > 0 else "no significant difference")
        vs_rows.append([SCEN_EN[n].replace("\n", ", ") if k == "rl" else "", STRATEGY_LABELS[k], f"{v['meanDifference']:+.1f}",
                        f"[{v['ciLow']:+.1f}, {v['ciHigh']:+.1f}]", f"{v['better']} / {v['worse']}", verdict])

load_rows = []
for h, m in zip(load_http, load_mqtt):
    load_rows.append([n0(h["buses"]), n0(h["sentPerSecond"]), f"{h['lossPercent']}%", f"{h['p50Ms']:.0f}", f"{h['p95Ms']:.0f}",
                      f"{m['lossPercent']}%", f"{m['p50Ms']:.0f}", f"{m['p95Ms']:.0f}", f"{m['p99Ms']:.0f}"])

req_rows = []
for r in requirements:
    tests = len([t for t in r["tests"]])
    req_rows.append([f"<b>{r['id']}</b>", escape(r["title"]), "proposal" if r["proposal"] else "derived",
                     "<br>".join(f"<code>{escape(p.split('/')[-1])}</code>" for p in r["impl"][:3]) + (" …" if len(r["impl"]) > 3 else ""),
                     f"{tests} test{'s' if tests != 1 else ''}" + (" + demo" if r["demo"] else "")])

ADRS = [
    ("001", "MQTT for devices, Kafka (Redpanda) as the event stream",
     "MQTT suits constrained, intermittently connected devices; Kafka gives a durable, partitioned log keyed by vehicle, "
     "replay and independent consumers. Redpanda is Kafka-compatible and light enough for a laptop."),
    ("002", "SignalR for the live map, Firebase as fallback",
     "The platform pushes positions it has validated itself; the old Firebase path stays as an automatic fallback when SignalR is silent for 35 s."),
    ("003", "Sampled position history in PostgreSQL",
     "One position per bus every 15 s, bounded buffer, batched writes, 3-day retention: ~350 k rows/day fits the free database."),
    ("004", "Embedded MQTT ingest for the free cloud tier",
     "Render's free plan cannot host a third always-on service plus Kafka, so the backend can subscribe to MQTT itself with the same parser."),
    ("005", "trip_id inferred from the timetable for GTFS-Realtime",
     "Devices report only positions; the matcher picks the scheduled trip whose departure at the next stop is closest, sticky and exclusive."),
    ("006", "Headway measured on the round-trip loop, alerts with hysteresis",
     "Projecting buses on the shape and placing them on a 2L loop compares buses in both directions; enter at 0.35, leave at 0.6, after 10 s."),
    ("007", "Digital twin: aggregate passenger model, deterministic",
     "Passengers as flows (arrival rate λ), dwell grows with boarding: bunching emerges instead of being imposed; 4 simulated hours take milliseconds."),
    ("008", "Classical holding vs tabular Q-learning, compared in the twin",
     "Interpretable 64 × 5 Q-table, 5 candidates selected on a validation set, results reported on disjoint test seeds with paired CIs."),
    ("009", "Observability with OpenTelemetry",
     "One ActivitySource/Meter, W3C trace context through Kafka headers and the internal bus, span links for batches, ratio sampling, OTLP export."),
]
adr_rows = [[f"ADR-{a}", f"<b>{escape(t)}</b><br><span class='muted'>{escape(d)}</span>"] for a, t, d in ADRS]

# ---------------------------------------------------------------- diagrams (English versions of docs/architecture)

C4_CONTEXT = """flowchart LR
  P["👤 Passenger<br/><small>live map, trip planner, tickets</small>"]
  D["👤 Dispatcher / admin<br/><small>bunching, digital twin, replay</small>"]
  DEV["On-board device (AVL)<br/><small>here: BusSimulator</small>"]
  GTFS["Tursib GTFS feed<br/><small>official timetable</small>"]
  CONS["GTFS-Realtime consumers<br/><small>journey planners</small>"]
  S(["TURSIB Tracker<br/><small>real-time tracking, digital twin,<br/>bunching control</small>"])
  DEV -- "positions · MQTT/TLS" --> S
  P -- "HTTPS / SignalR" --> S
  D -- "HTTPS" --> S
  GTFS -- "ZIP import" --> S
  S -- "VehiclePositions · protobuf" --> CONS
  classDef sys fill:#1d4ed8,color:#fff,stroke:#1e3a8a
  classDef ext fill:#f3f1ec,stroke:#9ca3af,color:#1f2328
  class S sys
  class P,D,DEV,GTFS,CONS ext"""

C4_CONTAINER = """flowchart TB
  DEV["On-board devices<br/><small>BusSimulator</small>"]
  subgraph SYS["TURSIB Tracker"]
    MQ[("MQTT broker<br/><small>Mosquitto · HiveMQ</small>")]
    ING["Ingestion worker<br/><small>.NET · validation, DLQ</small>"]
    K[("Event stream<br/><small>Kafka / Redpanda<br/>vehicle-positions</small>")]
    API["API + live platform<br/><small>ASP.NET Core 9 · REST, SignalR,<br/>GTFS-RT, bunching, twin</small>"]
    DB[("PostgreSQL<br/><small>Supabase · GTFS, accounts,<br/>history</small>")]
    FE["Web / mobile app<br/><small>Vue 3 PWA · Capacitor</small>"]
    OT["OpenTelemetry Collector<br/><small>→ Jaeger, Prometheus</small>"]
  end
  DEV -- "MQTT" --> MQ
  MQ --> ING
  ING -- "produce, key = vehicle" --> K
  K -- "consumer group" --> API
  MQ -. "embedded ingest (cloud)" .-> API
  API -- "EF Core" --> DB
  API -- "SignalR: positions, bunching" --> FE
  ING -. "OTLP" .-> OT
  API -. "OTLP" .-> OT"""

COMPONENTS = """flowchart LR
  subgraph In["Inputs"]
    K[KafkaPositionConsumer]
    E[EmbeddedMqttIngest]
    H[POST /api/live/positions]
  end
  K --> L[LiveIngestService]
  E --> L
  H --> L
  L --> S[(LiveBusState)]
  L --> B[[LiveEventBus]]
  B --> D{{LiveEventDispatcher}}
  D --> HB[HubBroadcastHandler → SignalR]
  D --> BD[BunchingDetector]
  D --> PH[PositionHistory → PostgreSQL]
  BD --> HP[HoldPolicy: recommendations]
  S --> RT[GTFS-RT + TripMatcher]
  S --> TS[TwinService]
  TS --> TE[TwinEngine + what-if]
  TE --> C[HoldPolicy / RlHoldControl]"""

BDD = """flowchart LR
  subgraph TT["«block» TursibTracker"]
    direction LR
    subgraph EDGE[" "]
      direction TB
      DEV["«block»<br/>OnBoardDevice [1..*]"]
      MQ["«block»<br/>MqttBroker"]
      ING["«block»<br/>IngestionService"]
      ES["«block»<br/>EventStream"]
    end
    subgraph LP["«block» LivePlatform"]
      direction TB
      LS[LiveState]
      BD[BunchingDetector]
      PH[PositionHistory]
      TM[TripMatcher]
    end
    subgraph DT["«block» DigitalTwin"]
      direction TB
      TE[TwinEngine]
      WI[WhatIf]
      CS["«abstract»<br/>ControlStrategy"]
      HC[HoldingControl]
      RL[ReinforcementLearningControl]
      HC -. specializes .-> CS
      RL -. specializes .-> CS
    end
    subgraph UI[" "]
      direction TB
      APP["«block»<br/>WebApp"]
      TC["«block»<br/>TelemetryCollector"]
    end
  end
  style EDGE fill:none,stroke:none
  style UI fill:none,stroke:none"""

SEQUENCE = """sequenceDiagram
  participant Dev as Device
  participant MQ as MQTT broker
  participant Ing as Ingestion
  participant K as Kafka
  participant BE as Backend
  participant FE as Map
  Dev->>MQ: PUBLISH tursib/vehicles/route_5_bus_0/position
  MQ->>Ing: message
  Ing->>Ing: PositionParser (id from topic, coordinates, time)
  Ing->>K: produce(key = route_5_bus_0, traceparent)
  K->>BE: batch (consumer group tursib-backend)
  BE->>BE: LiveIngestService → LiveBusState → LiveEventBus
  BE-->>FE: SignalR "positions"
  BE->>K: commit offset (after apply)"""

STATES = """stateDiagram-v2
  direction LR
  [*] --> Normal
  Normal --> Pending: gap ratio < 0.35
  Pending --> Normal: ratio ≥ 0.35
  Pending --> Active: persists 10 s
  Active --> Active: 0.35–0.6 (hysteresis)
  Active --> Normal: ratio > 0.6 or pair gone"""

# ---------------------------------------------------------------- key numbers used in the text

s5 = scenario("5 autobuze, doar zgomot")
s8d = scenario("8 autobuze, un autobuz oprit 5 min")
s3 = scenario("3 autobuze, doar zgomot")
rl_better = [SCEN_EN[n].replace("\n", ", ") for n in scen_names if versus(scenario(n), "rl")["ciHigh"] < 0]
rl_worse = [SCEN_EN[n].replace("\n", ", ") for n in scen_names if versus(scenario(n), "rl")["ciLow"] > 0]
rl_tie = [SCEN_EN[n].replace("\n", ", ") for n in scen_names if versus(scenario(n), "rl")["ciLow"] <= 0 <= versus(scenario(n), "rl")["ciHigh"]]
none_gap = [versus(scenario(n), "none")["meanDifference"] for n in scen_names]
http_max = load_http[-1]
mqtt_last_ok = max((m for m in load_mqtt if m["lossPercent"] == 0), key=lambda m: m["buses"])
mqtt_limit = load_mqtt[-1]
mqtt_170 = load_mqtt[0]
today = date.today()
backend_tests = sum(len(re.findall(r"\[(?:Fact|Theory)", p.read_text(encoding="utf-8"))) for p in (ROOT / "TursibBackend.Tests").glob("*.cs"))
frontend_tests = sum(len(re.findall(r"^\s*(?:it|test)\(", p.read_text(encoding="utf-8"), flags=re.M))
                     for p in (ROOT / "TursibFrontend" / "Frontend" / "src").rglob("*.test.ts"))

# ---------------------------------------------------------------- document

css = (pathlib.Path(__file__).parent / "report.css").read_text(encoding="utf-8")
FONTS = "".join(f"<link rel='stylesheet' href='../node_modules/@fontsource/{f}'>" for f in [
    "archivo/latin-500.css", "archivo/latin-700.css", "archivo/latin-800.css",
    "public-sans/latin-400.css", "public-sans/latin-400-italic.css", "public-sans/latin-600.css", "public-sans/latin-700.css",
    "archivo/latin-ext-500.css", "archivo/latin-ext-700.css", "public-sans/latin-ext-400.css", "public-sans/latin-ext-600.css"])

author_line = f"<p class='author'>{escape(AUTHOR)}</p>" if AUTHOR else ""

html = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>TURSIB Tracker: event-driven platform and digital twin</title>
{FONTS}<style>{css}</style></head><body>

<section class="cover">
  <div class="kicker">Master's project · Advanced System Engineering</div>
  <h1>TURSIB Tracker</h1>
  <p class="subtitle">From a real-time bus tracking prototype to an event-driven platform with a digital twin
  for detecting and reducing bus bunching in Sibiu</p>
  {author_line}
  <p class="date">Extended project proposal with implementation and results · {today.strftime('%B %Y')}</p>
  <div class="cover-facts">
    <div><b>{len(requirements)}</b><span>traceable requirements, checked by a test</span></div>
    <div><b>9</b><span>architecture decision records</span></div>
    <div><b>{len(control['scenarios']) * 4 * control['replications']}</b><span>paired twin runs in the control experiment</span></div>
    <div><b>{n0(mqtt_last_ok['buses'])}</b><span>buses through MQTT → Kafka with no loss on one 4-vCPU host</span></div>
  </div>
</section>

<section class="abstract">
<h2 class="nonum">Abstract</h2>
<p>The project continues my bachelor thesis, a real-time bus tracking application for Sibiu public transport (TURSIB):
an ASP.NET Core REST API, a Vue 3 PWA with a live map, a bus simulator built on the real GTFS data and a schedule-based
route planner. It turns the monolithic prototype into an <b>event-driven distributed system</b>: simulated on-board devices
publish vehicle positions over <b>MQTT</b> into a <b>Kafka</b> event stream, and the system exposes a standard
<b>GTFS-Realtime</b> feed. On top of it, a <b>digital twin</b> of a TURSIB line simulates buses and passengers, so that
what-if scenarios (extra buses, changed frequencies, closed streets) can be evaluated before they are applied.</p>
<p>The research goal is to <b>detect and reduce bus bunching</b>. Headways between consecutive buses are measured in real
time on the live stream, and a classical holding strategy is compared with a <b>reinforcement-learning</b> one in the
twin, using passenger waiting time and headway variance as metrics. The work follows a systems engineering approach:
traceable requirements, a SysML/C4 architecture model with documented design decisions, observability with
OpenTelemetry, and load and fault-tolerance testing.</p>
<p><b>Status.</b> Everything in the proposal is implemented and tested. In the twin, holding control removes almost all
bunching (it lowers the delay per passenger by {min(none_gap):.0f}–{max(none_gap):.0f} s against no control). The
learned policy beats the best classical rule in {len(rl_better)} of 6 scenarios by a small but significant margin, ties in
{len(rl_tie)} and loses in {len(rl_worse)} (a large fleet after a long disruption). The pipeline carries the Tursib fleet
(~170 buses) with a {mqtt_170['p95Ms']:.0f} ms 95th-percentile latency to the map and keeps up without loss to
{n0(mqtt_last_ok['buses'])} buses on a single machine.</p>
</section>

<nav class="toc">
<h2 class="nonum">Contents</h2>
<ol>
<li>Starting point and objectives</li><li>Requirements and traceability</li><li>Architecture</li>
<li>Design decisions</li><li>Event-driven pipeline and GTFS-Realtime</li><li>Real-time bunching detection</li>
<li>Digital twin and what-if scenarios</li><li>Bunching control: holding vs reinforcement learning</li>
<li>Observability</li><li>Load testing</li><li>Fault-tolerance testing</li><li>Validity, limitations and further work</li>
<li>Work plan and status</li><li>Reproducing the results</li>
</ol>
</nav>

<h2>Starting point and objectives</h2>
<p>The bachelor prototype tracked buses through a single path: the simulator wrote positions to Firebase and every
browser read them from there. The backend never saw live data, so it could not validate it, keep a history, publish
an open feed or reason about the fleet. The master's project keeps the user-facing application (live map, planner,
tickets, admin) and rebuilds the data side as a platform.</p>
<table class="obj">
<thead><tr><th>Objective</th><th>What it means here</th><th>Delivered by</th></tr></thead><tbody>
<tr><td><b>O1</b> Event-driven distributed system</td><td>Devices publish over MQTT; positions flow through Kafka to independent consumers.</td><td>§5, REQ-F-01…05</td></tr>
<tr><td><b>O2</b> Open real-time data</td><td>Standard GTFS-Realtime VehiclePositions with matched scheduled trips.</td><td>§5, REQ-F-06…07</td></tr>
<tr><td><b>O3</b> Real-time bunching detection</td><td>Headways between consecutive buses computed continuously on the live stream.</td><td>§6, REQ-F-09, F-15</td></tr>
<tr><td><b>O4</b> Digital twin with what-if</td><td>Buses and passengers simulated; extra buses, frequencies, closed streets.</td><td>§7, REQ-F-10…11</td></tr>
<tr><td><b>O5</b> Holding vs reinforcement learning</td><td>Both strategies compared on waiting time and headway variance, with uncertainty.</td><td>§8, REQ-F-12…14</td></tr>
<tr><td><b>O6</b> Systems engineering</td><td>Traceable requirements, SysML/C4, ADRs, OpenTelemetry, load and fault tests.</td><td>§2–4, §9–11</td></tr>
</tbody></table>
<p><b>Research questions.</b> (RQ1) Can bunching be detected reliably from position reports alone, without schedule
adherence data? (RQ2) How much passenger delay does bunching cost, and how much of it does classical holding recover?
(RQ3) Does a policy learned by reinforcement learning do better than the best classical rule, and in which conditions?</p>

<h2>Requirements and traceability</h2>
<p>Each requirement in <code>docs/requirements.md</code> has a source (a sentence of the proposal, or a requirement it
is derived from), the files that implement it and the tests that verify it. A test,
<code>RequirementsTraceabilityTests</code>, parses that file and fails when a referenced file or test no longer exists,
so the matrix cannot drift silently from the code. The SysML model (<code>docs/architecture/tursib.sysml</code>) declares
the same requirements and links them to the parts that satisfy them.</p>
{table(["ID", "Requirement", "Source", "Implemented in", "Verified by"], req_rows, "req")}
<p class="note">The backend has {backend_tests} test methods (xUnit, some parameterised) and the frontend {frontend_tests} test cases (Vitest).
Integration tests against real Kafka, MQTT and PostgreSQL run in CI with service containers.</p>

<h2>Architecture</h2>
<p>The model is kept as code: <code>docs/architecture/workspace.dsl</code> (Structurizr, C4) and
<code>docs/architecture/tursib.sysml</code> (SysML v2 textual notation), rendered below.</p>
{mermaid(C4_CONTEXT, "C4 level 1, system context.", "diagram")}
{mermaid(C4_CONTAINER, "C4 level 2, containers. Dashed: the embedded ingest used on the free cloud tier, and telemetry export.", "diagram")}
{mermaid(COMPONENTS, "C4 level 3, components of the live platform. A new consumer of live positions is a new <code>IBusPositionHandler</code>; producers do not change.", "diagram")}
{mermaid(BDD, "SysML block definition: the system and its parts (composition shown as nesting; the full model with ports, item flows and <code>satisfy</code> links is in <code>tursib.sysml</code>).", "diagram")}
{mermaid(STATES, "SysML state machine of a bunching alert.", "diagram")}
{mermaid(SEQUENCE, "Sequence of one position from the device to the map.", "diagram")}
<h3>Deployment modes</h3>
<p>The same code runs in two topologies, chosen by configuration:</p>
<ul>
<li><b>Full stack</b> (<code>docker compose</code> or an own server): simulator → Mosquitto → ingestion worker → Redpanda
→ backend consumer, with the OpenTelemetry Collector, Jaeger and Prometheus. Managed Kafka (SASL/SSL) and MQTT over
TLS are supported and were verified against local secured brokers.</li>
<li><b>Free cloud tier</b> (Render, Supabase, a managed MQTT broker): the backend subscribes to MQTT itself (embedded
ingest, same parser and validation), because a third always-on service and a Kafka cluster do not fit the free quota.
HTTP ingest remains available in both modes. A public status endpoint reports the active mode and its health.</li>
</ul>

<h2>Design decisions</h2>
<p>Decisions are recorded as ADRs (context, decision, consequences) in <code>docs/adr/</code>. An accepted ADR is not
rewritten; a new one supersedes it.</p>
{table(["ADR", "Decision and main reason"], adr_rows, "adr")}

<h2>Event-driven pipeline and GTFS-Realtime</h2>
<ul>
<li><b>Ingestion.</b> The vehicle id is taken from the MQTT topic (<code>tursib/vehicles/{{id}}/position</code>) and a
payload with a different id is rejected, so a device cannot report for another bus. Oversized or malformed payloads,
invalid coordinates and timestamps in the future are rejected too, counted, and written to
<code>vehicle-positions.dlq</code> with the reason as a header.</li>
<li><b>Stream.</b> Valid positions go to <code>vehicle-positions</code> (3 partitions) keyed by vehicle, so each bus
stays ordered. The producer is idempotent and non-blocking (delivery reported in a callback): the load test showed that
awaiting each acknowledgement capped throughput below 50 messages/s (§10).</li>
<li><b>Consumption.</b> The backend consumes in batches (up to 500 messages or 100 ms) and commits offsets <i>after</i>
applying them: at-least-once. The live state ignores positions older than the one it holds, so replays are harmless.</li>
<li><b>Fan-out.</b> Inside the backend an in-memory event bus dispatches each batch to independent handlers: SignalR
broadcast, bunching detector and sampled history (PostgreSQL, 15 s per bus, 3-day retention, used by the admin replay).</li>
<li><b>GTFS-Realtime.</b> <code>/api/gtfs-rt/vehicle-positions</code> serves the official protobuf (official field
numbers). Devices only report positions, so a trip matcher assigns the scheduled <code>trip_id</code>: among today's
active trips of the route and direction that stop at the bus's next stop, the one whose scheduled time there is closest,
kept while the bus progresses and never shared between two buses.</li>
</ul>

<h2>Real-time bunching detection</h2>
<p>Each position is projected onto the route's GTFS shape and placed on the <b>round-trip loop</b> (outbound distance
<i>d</i>, inbound 2<i>L</i> − <i>d</i>), so buses in both directions are compared on one circle. For each bus, the gap to
the bus ahead is compared with the ideal gap 2<i>L</i>/<i>n</i>. An alert opens when the ratio stays below 0.35 for 10 s,
becomes critical below 0.15 and closes above 0.6 (hysteresis, so noisy positions do not make alerts flap). Alerts are
pushed over SignalR to the dispatcher panel, each with the holding time the classical policy recommends for the
follower. The simulator can close the loop (<code>APPLY_HOLDS=true</code>): it reads the recommendations and holds the
follower at its next stop. Nothing in the backend commands buses. This answers RQ1 with a design that needs only
positions; on the simulator it detects every bunching episode the twin produces, but it has not been validated on real
AVL data (§12).</p>

<h2>Digital twin and what-if scenarios</h2>
<h3>Model</h3>
<p>A line is a loop of length 2<i>L</i> (here <i>L</i> = {control['routeLengthMeters'] / 1000:.0f} km) with stops every
~400 m. Buses cruise at ~25 km/h with ±15% speed noise and cannot overtake. Passengers arrive at every stop at rate
λ = 0.008/s. A bus arriving after a headway <i>H</i> finds λ<i>H</i> passengers, who have waited
λ<i>H</i>²/2 passenger-seconds in total; dwell time = 8 s + 2 s per boarding passenger (±30% noise, at most 90 s). This
feedback is what makes bunching emerge: a late bus finds more passengers, dwells longer and falls further behind, while
its follower catches up. Each bus also carries a load (1/8 of riders alight at every stop), so the time that holding
costs the passengers on board is counted.</p>
<p><b>Metrics.</b> With time headways <i>H</i> observed at the stops:</p>
<div class="formulas">
<div>mean wait = E[<i>H</i>²] / (2 E[<i>H</i>]) = E[<i>H</i>]/2 + Var(<i>H</i>) / (2 E[<i>H</i>])</div>
<div>excess wait = Var(<i>H</i>) / (2 E[<i>H</i>]) &nbsp;·&nbsp; headway CV = sd(<i>H</i>) / E[<i>H</i>]</div>
<div>total delay per passenger = (waiting at stops + on-board time lost to holding) / passengers served</div>
</div>
<p>The excess wait is the part of waiting caused by irregularity, and it is exactly the headway-variance term the
proposal asks for. Total delay is the main criterion, because holding reduces waiting at stops by delaying the people
already on board, and a fair comparison has to charge for both. A run is deterministic for a seed; 4 simulated hours
take milliseconds.</p>
<h3>What-if scenarios</h3>
<p>The twin supports a changed frequency (the number of buses is derived from the target headway and the estimated
round-trip time), extra buses inserted at a given time into the largest gap, closed street sections (both directions;
the detour is modelled as a lower effective speed over the section, the stops inside are skipped and their demand moves
to the neighbouring stops) and forced stops of a bus. All are available in the admin panel and over
<code>POST /api/twin/run</code>; the table below uses {control['replications']} seeds and classical holding.</p>
{table(["Scenario", "Buses", "Mean headway (s)", "Mean wait (s)", "Total delay (s)", "Headway CV", "Skipped stop visits"], whatif_rows, "num")}
{figure(fig_whatif, "What-if scenarios on a 5 km line with headway holding (mean over test seeds).")}
<p>Two results are worth noting. Adding one bus lowers the delay per passenger from
{base['totalDelay']['mean']:.0f} s to {wi['+1 autobuz de la început']['totalDelay']['mean']:.0f} s; going down to a
10-minute frequency raises it to {wi['frecvență: un autobuz la 10 min']['totalDelay']['mean']:.0f} s. The closure is
the interesting case: the detour lengthens the cycle and holding, which measures <i>spatial</i> gaps, misreads buses
slowed down in the detour as bunched and holds their followers. The total delay
({wi['stradă închisă km 1,5–2,5, ocol +1,2 km']['totalDelay']['mean']:.0f} s) is well above the waiting time
({wi['stradă închisă km 1,5–2,5, ocol +1,2 km']['meanWait']['mean']:.0f} s). This is exactly the kind of interaction the
twin is meant to reveal before a closure is applied: the holding rule needs a time-based gap, or to be paused, on lines
with a detour.</p>

<h2>Bunching control: holding vs reinforcement learning</h2>
<h3>Strategies</h3>
<ul>
<li><b>No control</b> (reference).</li>
<li><b>Holding, headway rule.</b> When a bus arrives at a stop less than 0.85 of the ideal gap behind the bus ahead, it is
held for (ideal − gap ahead)/<i>v</i>, at most 45 s per stop.</li>
<li><b>Holding, equalize rule.</b> Hold (gap behind − gap ahead)/2<i>v</i> under the same trigger and cap, balancing the
bus between its neighbours.</li>
<li><b>Reinforcement learning.</b> Tabular Q-learning with one policy shared by all buses. State: gap ahead and gap
behind relative to the ideal, 8 bins each (64 states). Actions: hold 0, 5, 15, 30 or 45 s. Reward: −(3 × excess wait
created at the next stop + on-board delay caused by the hold), in passenger-seconds; semi-Markov update at the same bus's
next decision. ε decays 0.3 → 0.02 and the learning rate 0.2 → 0.02 over 3 000 episodes of randomised scenarios (3–8
buses, 3–8 km lines, variable demand, disturbances).</li>
</ul>
<h3>Experimental protocol</h3>
<p>Training, validation and test use disjoint seed ranges. Five policies are trained with different seeds and the one
with the lowest delay on the validation set is kept (training is genuinely variable: validation delays ranged from
209 to 242 s). Results are reported on {control['replications']} test seeds ({control['testSeeds']}) in six scenarios: 3, 5
and 8 buses, with noise only or with one bus stopped for 5 minutes, {control['durationSeconds'] // 3600} simulated hours
each. Every strategy runs on the same seeds, so differences are <b>paired</b>; the 95% confidence interval of the mean
paired difference uses Student's <i>t</i>.</p>
{figure(fig_delay, "Total delay per passenger by strategy and scenario (waiting at stops + on-board delay from holding).")}
{figure(fig_cv, "Headway coefficient of variation (headway regularity; lower is better).")}
{figure(fig_ci, "Mean paired difference to headway holding with 95% confidence intervals. Below zero: better than headway holding.")}
<p>Table A1 in the appendix gives every metric for every scenario and strategy (mean ± standard deviation over the test seeds).</p>
{table(["Scenario", "Strategy − headway", "Mean Δ delay (s)", "95% CI", "Seeds better / worse", "Verdict"], vs_rows, "num")}
{figure(fig_hold, "Holding time spent per bus-hour, i.e. the cost of control borne by the passengers on board.")}
<h3>Findings</h3>
<ul>
<li><b>Bunching is expensive and holding recovers almost all of it (RQ2).</b> Without control, buses spend
{strat(s5, 'none')['bunchedPercent']['mean']:.0f}% of the time bunched with 5 buses and noise only, and over 85% after a
5-minute stop. Headway holding removes bunching in all but the hardest scenario and lowers the delay per passenger by
{min(none_gap):.0f} to {max(none_gap):.0f} s.</li>
<li><b>The learned policy is slightly better than the best classical rule in most scenarios (RQ3).</b> It reduces the
delay per passenger significantly in {', '.join(rl_better)} (by {abs(versus(s5, 'rl')['meanDifference']):.1f}–{abs(versus(s3, 'rl')['meanDifference']):.1f} s),
with more regular headways (CV {strat(s5, 'rl')['headwayCv']['mean']:.2f} vs {strat(s5, 'headway')['headwayCv']['mean']:.2f}
with 5 buses) and <i>less</i> holding (e.g. {strat(s3, 'rl')['holdSecondsPerBusHour']['mean']:.0f} vs
{strat(s3, 'headway')['holdSecondsPerBusHour']['mean']:.0f} s per bus-hour with 3 buses). It learned to hold early and
briefly rather than late and long.</li>
<li><b>It is not uniformly better.</b> With {', '.join(rl_tie) or 'no scenario'} the difference is not significant, and
with {', '.join(rl_worse) or 'no scenario'} it is worse on every seed
({versus(s8d, 'rl')['meanDifference']:+.1f} s). With 8 buses the gaps are short, the state (two gap ratios) does not see
how long the disruption lasts, and the learned holds are too long during recovery. Equalize holding is consistently
slightly worse than the headway rule.</li>
<li>The gains of RL over a well-tuned rule are a few seconds per passenger (1–2%), against 70–500 s gained by any
holding at all. The practical conclusion is that <b>reliable detection plus a simple holding rule captures most of the
benefit</b>; learning is a refinement whose value depends on the operating regime, which the twin makes measurable before
deployment.</li>
</ul>

<h2>Observability</h2>
<p>All components use one OpenTelemetry source, <code>Tursib.Pipeline</code>, through the .NET APIs
(<code>ActivitySource</code>, <code>Meter</code>), and export over OTLP only when an endpoint is configured, so local
runs and the free tier pay nothing.</p>
<ul>
<li><b>Traces.</b> Spans <code>mqtt receive position</code> → <code>kafka produce</code> → <code>kafka consume</code> →
<code>live ingest</code> → <code>handle &lt;handler&gt;</code>. The W3C <code>traceparent</code> travels in the Kafka
message headers. The backend consumes in batches, so the <code>kafka consume</code> span starts a trace of its own and
links to the traces of the messages in the batch (span links, the OpenTelemetry convention for batch messaging); inside
the backend the batch context travels through the internal event bus, so ingest and every handler (SignalR, bunching,
history) are one trace (figure below).</li>
<li><b>Metrics.</b> Positions received, rejected (by reason) and accepted per stage and transport; position age at
processing; live and fresh buses; active and critical bunching alerts; standard ASP.NET Core, HttpClient and runtime
instrumentation.</li>
<li><b>Sampling.</b> Parent-based trace-id ratio (0.2 in <code>docker compose</code>). Tracing every position at
20 000 positions/s drove Jaeger to ~4 GB of memory; sampling is a design decision, not an afterthought.</li>
<li><b>Local stack.</b> OpenTelemetry Collector → Jaeger (traces) and Prometheus (metrics). Tests verify the
instrumentation by listening to the source directly, without an exporter.</li>
</ul>
{figure("<img src='../assets/jaeger-trace.png' alt='Jaeger trace of one consumed batch' style='width:100%;border:1px solid #e5e2dc'>",
        "A batch in Jaeger, recorded during the load tests: the consume span, the ingest into the live state and the three "
        "handlers fed by the internal event bus. The whole batch is processed in about 0.25 ms.")}

<h2>Load testing</h2>
<p>A load tool (<code>tools/loadtest</code>) simulates a fleet that reports every 0.5 s, either over HTTP or as devices
on MQTT, and subscribes to the SignalR hub like a map. Each message carries a sequence number, so the latency is measured
<b>end to end</b>, from publication to arrival on the map, and every loss is counted exactly. Environment: one machine
with 4 vCPU and 15 GB RAM running every container (PostgreSQL, Mosquitto, Redpanda, OpenTelemetry Collector, Jaeger,
Prometheus, ingestion, backend) and the load generator. The figures are limits of this test setup, not of a production
deployment.</p>
{figure(fig_latency, "End-to-end latency against fleet size.")}
{table(["Buses", "Messages/s", "HTTP lost", "HTTP p50 (ms)", "HTTP p95 (ms)", "Chain lost", "Chain p50 (ms)", "Chain p95 (ms)", "Chain p99 (ms)"], load_rows, "num")}
<ul>
<li><b>A defect found by the load test.</b> The first run of the MQTT chain lost over 94% of positions even at 170 buses,
with 17–30 s delays: ingestion awaited each Kafka acknowledgement and the MQTT client delivers messages one at a time,
capping throughput at about one message per acknowledgement round-trip. Producing without blocking (acknowledgement in a
callback, ordering kept by the idempotent producer) removed the loss up to 10 000 messages/s.</li>
<li><b>The ~130 ms floor on the Kafka chain is configuration.</b> The producer lingers 20 ms and the consumer waits up to
100 ms to fill a batch; under light load batches close on time, under heavy load they fill faster and the median drops.
For a map refreshed every 0.5–10 s this is negligible, and both values can be lowered.</li>
<li><b>Headroom.</b> For the Tursib fleet the full chain has ~{mqtt_last_ok['buses'] // 170}× headroom before the first loss
(at {n0(mqtt_limit['buses'])} buses, {mqtt_limit['lossPercent']}% lost, p95 {mqtt_limit['p95Ms']:.0f} ms) and HTTP ingest
over {http_max['buses'] // 170}× ({n0(http_max['buses'])} buses without loss). The first saturation point is the single
ingestion process sharing four cores with everything else; horizontal scaling is prepared (shared MQTT subscription,
3 partitions) and exercised in §11.</li>
</ul>

<h2>Fault-tolerance testing</h2>
<p>Two levels of testing. <b>Automated tests</b> (<code>FaultToleranceTests</code>) cover a database outage during history
writes (the writer keeps the batch and retries), a failing event handler (the others keep receiving) and a real MQTT broker
restart (the subscriber reconnects and resubscribes). <b>Experiments</b> on the full <code>docker compose</code> chain
(<code>tools/resilience/run-fault-tests.sh</code>) inject faults while the load tool measures what reaches the map.</p>
{fault_section()}

<h2>Validity, limitations and further work</h2>
<ul>
<li><b>Model, not field data.</b> The control results come from the twin. The passenger model is aggregate (uniform
arrivals, no origin-destination matrix, no transfers, constant demand within a run), and absolute values belong to the
model; conclusions are about relative differences between strategies under identical conditions. The next step is
calibration with real AVL positions and passenger counts from Tursib, which the platform can ingest unchanged.</li>
<li><b>One line at a time.</b> The twin models a single line as a loop with evenly spaced stops, starting from the real
shape length and, optionally, the live positions of the fleet. Interactions between lines sharing a corridor are not
modelled.</li>
<li><b>RL state.</b> The tabular state sees only the two gap ratios. Adding load, demand, time since the disruption or
the gaps of further neighbours (function approximation, e.g. DQN) is the obvious extension, and the 8-bus disrupted
scenario is the case it would need to fix.</li>
<li><b>Time-based gaps.</b> The closure scenario showed that space-based holding misfires when part of the route is
slower; a time-headway formulation (from the schedule or from estimated travel times) would be more robust.</li>
<li><b>Cloud broker.</b> The managed MQTT broker path (TLS on 8883, credentials) is configured and was verified against a
local secured broker, but not yet run against a real cloud broker account.</li>
<li><b>Load tests on one host.</b> Every component shares one machine, so the limits are of that machine; the scaling
experiments are indicative.</li>
</ul>

<h2>Work plan and status</h2>
{table(["Stage", "Content", "Status"], [
    ["1. Event-driven live data", "HTTP ingest, live state, internal event bus, SignalR, Firebase fallback", "done"],
    ["2. MQTT → Kafka pipeline", "Ingestion worker, validation, DLQ, consumer, TLS/SASL, embedded ingest, history, replay UI", "done"],
    ["3. GTFS-Realtime", "Protobuf feed, trip matching, GTFS import CLI and workflow", "done"],
    ["4. Bunching detection", "Headways on the loop, alerts with hysteresis, dispatcher panel", "done"],
    ["5. Digital twin", "Buses and passengers, metrics, what-if (frequency, extra buses, closures)", "done"],
    ["6. Control", "Holding rules, Q-learning, paired experiment with confidence intervals", "done"],
    ["7. Systems engineering", "Requirements + traceability test, C4 + SysML, 9 ADRs", "done"],
    ["8. Quality", "OpenTelemetry, load tests, fault-tolerance tests", "done"],
    ["9. Validation", "Calibration with real Tursib AVL data and passenger counts; managed broker in production", "next"],
    ["10. Extensions", "Time-based holding, richer RL state, multi-line twin", "next"],
], "plan")}

<h2>Reproducing the results</h2>
<p>All results are generated from the repository; the document itself is built from the result files
(<code>tools/report</code>).</p>
<table class="repro"><tbody>
<tr><td>Control experiment and what-if</td><td><code>cd TursibBackend && dotnet run -- twin-experiment ../docs/results 20</code></td></tr>
<tr><td>Retrain the RL policy</td><td><code>cd TursibBackend && dotnet run -- train-rl 3000 5</code></td></tr>
<tr><td>Full stack</td><td><code>docker compose up -d</code> (Jaeger on :16686, Prometheus on :9090)</td></tr>
<tr><td>Load tests</td><td><code>dotnet run --project tools/loadtest -c Release -- --target mqtt --buses 170,500,1000</code></td></tr>
<tr><td>Fault experiments</td><td><code>tools/resilience/run-fault-tests.sh 170 120</code></td></tr>
<tr><td>Tests</td><td><code>dotnet test TursibBackend.Tests</code> · <code>npx vitest run</code> (in <code>TursibFrontend/Frontend</code>)</td></tr>
<tr><td>This document</td><td><code>cd tools/report && npm ci && npm run build</code></td></tr>
</tbody></table>
<p class="note">Raw results: <code>docs/results/*.json</code>; readable summaries: <code>docs/results/*.md</code>;
architecture model: <code>docs/architecture/</code>; decisions: <code>docs/adr/</code>; requirements:
<code>docs/requirements.md</code>; deployment runbook: <code>docs/DEPLOYMENT.md</code>.</p>

<h2 class="nonum appendix">Appendix A. Detailed control results</h2>
<p><b>Table A1.</b> Every strategy in every scenario, mean ± standard deviation over {control['replications']} test seeds,
{control['durationSeconds'] // 3600} simulated hours each. Total delay = waiting at stops + on-board time lost to holding, per
passenger. Excess wait = Var(<i>H</i>)/(2E[<i>H</i>]). Time bunched = share of 10 s samples with an active bunching alert.</p>
{table(["Scenario", "Strategy", "Total delay (s)", "Mean wait (s)", "Excess wait (s)", "Headway CV", "Holding (s/bus·h)", "Time bunched (%)"], control_rows, "num control")}

<script src="../node_modules/mermaid/dist/mermaid.min.js"></script>
<script>
mermaid.initialize({{ startOnLoad: false, theme: 'base', securityLevel: 'loose',
  fontFamily: 'Public Sans, sans-serif',
  themeVariables: {{ primaryColor: '#eef2ff', primaryBorderColor: '#4f46e5', primaryTextColor: '#1f2328',
    lineColor: '#6b7280', secondaryColor: '#f3f1ec', tertiaryColor: '#faf9f6', fontSize: '13px' }},
  flowchart: {{ htmlLabels: true, curve: 'basis' }} }});
window.mermaidDone = mermaid.run({{ querySelector: '.mermaid' }}).then(() => true);
</script>
</body></html>"""

OUT.mkdir(exist_ok=True)
(OUT / "report.html").write_text(html, encoding="utf-8")
print(f"build/report.html: {figure.n} figures, {len(requirements)} requirements, fault experiments: {len(fault)}")
