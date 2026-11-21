"""Rezumatul experimentelor de toleranță la erori, din fișierele JSON scrise de tools/loadtest."""
import json, sys, pathlib

DESCRIPTIONS = {
    "baseline": "fără defecțiune (referință)",
    "kafka-down": "Kafka (Redpanda) oprit 30 s",
    "mqtt-broker-restart": "brokerul MQTT repornit",
    "backend-restart": "backend-ul repornit",
    "ingestion-down-1-instance": "ingestia oprită 30 s (o singură instanță)",
    "ingestion-down-2-instances": "una din două instanțe de ingestie oprită (abonament MQTT partajat)",
}

def analyse(step, fault_at=30):
    tl = step["timeline"]
    expected = step["buses"] * 1000 / step["intervalMs"]
    after = [s for s in tl if s["second"] >= fault_at]
    outage = [s for s in after if s["received"] < 0.5 * expected and s["second"] < step["durationSeconds"]]
    outage_s = len(outage)
    recovered = None
    if outage:
        last_bad = max(s["second"] for s in outage)
        good = [s for s in tl if s["second"] > last_bad and s["received"] >= 0.8 * expected]
        recovered = good[0]["second"] - fault_at if good else None
    p95s = [s["p95Ms"] for s in tl if s["p95Ms"] == s["p95Ms"]]  # fără NaN
    return outage_s, recovered, max(p95s) if p95s else float("nan")

def main(folder):
    rows = []
    for path in sorted(pathlib.Path(folder).glob("*.json")):
        step = json.loads(path.read_text())[0]
        outage, recovered, worst_p95 = analyse(step)
        rows.append((path.stem, step, outage, recovered, worst_p95))
    order = list(DESCRIPTIONS)
    rows.sort(key=lambda r: order.index(r[0]) if r[0] in order else 99)

    print("# Rezultate: toleranță la erori\n")
    print("Generat cu `tools/resilience/run-fault-tests.sh` pe lanțul complet din `docker compose` (MQTT → ingestie → Kafka → backend → SignalR).")
    if rows:
        s = rows[0][1]
        print(f"Flotă sintetică de {s['buses']} autobuze la {s['intervalMs']} ms (MQTT QoS 0), {s['durationSeconds']} s per experiment; defecțiunea începe la secunda 30.")
    print("„Pierdute” = poziții trimise care nu au ajuns pe hartă (SignalR). Întreruperea = secunde cu sub jumătate din debitul așteptat; revenirea = secunde de la defecțiune până la debit ≥ 80%.\n")
    print("| experiment | trimise | pe hartă | pierdute | întrerupere | revenire după | latență p95 maximă | erori la publicare |")
    print("|---|---|---|---|---|---|---|---|")
    for name, s, outage, recovered, p95 in rows:
        desc = DESCRIPTIONS.get(name, name)
        rec = f"{recovered} s" if recovered is not None else ("—" if outage == 0 else "nu a revenit")
        print(f"| {desc} | {s['sent']} | {s['received']} | {s['lossPercent']}% | {outage} s | {rec} | {p95/1000:.1f} s | {s['publishErrors']} |")

if __name__ == "__main__":
    main(sys.argv[1])
