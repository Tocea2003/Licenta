#!/usr/bin/env bash
# Experimente de toleranță la erori pe lanțul complet (docker compose): o flotă sintetică publică pe MQTT,
# iar la secunda 30 se injectează o defecțiune. Instrumentul de încărcare măsoară, secundă cu secundă, câte poziții
# ajung pe hartă (SignalR) și cu ce latență. Rezumatul: docs/results/fault-tolerance.md.
#
# Rulare (din rădăcina repo-ului, cu stack-ul pornit: docker compose up -d db mosquitto redpanda otel-collector jaeger prometheus ingestion backend):
#   tools/resilience/run-fault-tests.sh [autobuze] [durată s]
set -euo pipefail
cd "$(dirname "$0")/../.."
BUSES=${1:-170}
DURATION=${2:-120}
OUT=docs/results/fault
mkdir -p "$OUT"
LOAD="dotnet run --project tools/loadtest -c Release --no-build --"

wait_healthy() {
  for _ in $(seq 1 60); do curl -sf -m 2 localhost:5022/api/live/status >/dev/null && return 0; sleep 1; done
  echo "backend-ul nu răspunde" >&2; return 1
}

# experiment <nume> <comandă defecțiune> <comandă revenire> [secunde până la revenire]
experiment() {
  local name=$1 fault=$2 recover=$3 down=${4:-30}
  echo "=== $name"
  wait_healthy
  $LOAD --target mqtt --buses "$BUSES" --duration "$DURATION" --drain 15 --out "$OUT" --name "$name" > "$OUT/$name.log" 2>&1 &
  local pid=$!
  sleep 30
  echo "    defecțiune: $fault"; eval "$fault"
  sleep "$down"
  echo "    revenire: $recover"; eval "$recover"
  wait $pid
  tail -2 "$OUT/$name.log"
}

experiment baseline "true" "true"
experiment kafka-down "docker compose stop redpanda" "docker compose start redpanda"
experiment mqtt-broker-restart "docker compose restart mosquitto" "true" 0
experiment backend-restart "docker compose restart backend" "true" 0
experiment ingestion-down-1-instance "docker compose stop ingestion" "docker compose start ingestion"

# redundanță: două instanțe de ingestie în abonament MQTT partajat ($share/ingestion/...); una cade
docker compose up -d --no-build --scale ingestion=2 ingestion
sleep 10
experiment ingestion-down-2-instances "docker kill \$(docker compose ps -q ingestion | head -1)" "docker compose up -d --no-build --scale ingestion=2 ingestion"
docker compose up -d --no-build --scale ingestion=1 ingestion

python3 tools/resilience/summarize.py "$OUT" > docs/results/fault-tolerance.md
echo "✅ docs/results/fault-tolerance.md"
