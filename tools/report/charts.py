"""SVG charts for the project document, drawn directly (no matplotlib), in the palette of the app."""
from html import escape

INK = "#1f2328"
MUTED = "#6b7280"
GRID = "#e5e2dc"
STRATEGY_COLORS = {"none": "#be123c", "headway": "#2563eb", "equalize": "#0f766e", "rl": "#7c3aed"}
STRATEGY_LABELS = {"none": "no control", "headway": "headway holding", "equalize": "equalize holding", "rl": "RL (Q-learning)"}
FONT = "font-family:'Public Sans',sans-serif"


def _nice_max(v):
    for exp in range(-2, 7):
        for step in (1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8):
            m = step * 10 ** exp
            if m >= v:
                return m
    return v


def _ticks(vmax, n=5):
    return [vmax * i / n for i in range(n + 1)]


def _fmt(v):
    return f"{v:,.0f}".replace(",", " ") if abs(v) >= 10 else f"{v:.1f}".rstrip("0").rstrip(".")


def legend(items, x, y):
    out, cx = [], x
    for label, color in items:
        out.append(f'<rect x="{cx}" y="{y - 9}" width="12" height="12" rx="2" fill="{color}"/>')
        out.append(f'<text x="{cx + 17}" y="{y + 1}" font-size="11" fill="{INK}" style="{FONT}">{escape(label)}</text>')
        cx += 30 + len(label) * 6.2
    return "".join(out)


def grouped_bars(groups, series, values, errors=None, title="", unit="", width=720, height=300, colors=None, labels=None):
    """groups: x-axis labels; series: keys; values[g][s] -> float; errors[g][s] optional (drawn as ± bars)."""
    colors = colors or STRATEGY_COLORS
    left, right, top, bottom = 56, 12, 34, 64
    pw, ph = width - left - right, height - top - bottom
    vmax = _nice_max(max(values[g][s] + (errors[g][s] if errors else 0) for g in groups for s in series) * 1.05)
    gw = pw / len(groups)
    bw = min(22, (gw - 16) / len(series))
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="100%" role="img" aria-label="{escape(title)}">']
    for t in _ticks(vmax):
        y = top + ph - t / vmax * ph
        out.append(f'<line x1="{left}" x2="{width - right}" y1="{y:.1f}" y2="{y:.1f}" stroke="{GRID}"/>')
        out.append(f'<text x="{left - 6}" y="{y + 4:.1f}" font-size="10" text-anchor="end" fill="{MUTED}" style="{FONT}">{_fmt(t)}</text>')
    out.append(f'<text x="{left}" y="{top - 14}" font-size="11" fill="{MUTED}" style="{FONT}">{escape(unit)}</text>')
    for gi, g in enumerate(groups):
        gx = left + gi * gw + (gw - bw * len(series)) / 2
        for si, s in enumerate(series):
            v = values[g][s]
            h = v / vmax * ph
            x = gx + si * bw
            out.append(f'<rect x="{x:.1f}" y="{top + ph - h:.1f}" width="{bw - 2:.1f}" height="{h:.1f}" fill="{colors[s]}" rx="1.5"/>')
            if errors:
                e = errors[g][s] / vmax * ph
                cx = x + (bw - 2) / 2
                out.append(f'<line x1="{cx:.1f}" x2="{cx:.1f}" y1="{top + ph - h - e:.1f}" y2="{top + ph - h + e:.1f}" stroke="{INK}" stroke-width="1"/>')
        lines = g.split("\n")
        for li, line in enumerate(lines):
            out.append(f'<text x="{left + gi * gw + gw / 2:.1f}" y="{top + ph + 16 + li * 13}" font-size="10.5" text-anchor="middle" fill="{INK}" style="{FONT}">{escape(line)}</text>')
    out.append(legend([((labels or STRATEGY_LABELS).get(s, s), colors[s]) for s in series], left, height - 10))
    out.append("</svg>")
    return "".join(out)


def ci_chart(groups, series, diffs, title="", width=720, height=280):
    """diffs[g][s] = (mean, low, high): difference to the reference with its confidence interval."""
    left, right, top, bottom = 56, 12, 20, 64
    pw, ph = width - left - right, height - top - bottom
    lo = min(d[1] for g in groups for s in series for d in [diffs[g][s]])
    hi = max(d[2] for g in groups for s in series for d in [diffs[g][s]])
    span = max(abs(lo), abs(hi)) * 1.15
    vmin, vmax = -span, span
    y = lambda v: top + ph - (v - vmin) / (vmax - vmin) * ph
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="100%" role="img" aria-label="{escape(title)}">']
    for t in [vmin + (vmax - vmin) * i / 6 for i in range(7)]:
        out.append(f'<line x1="{left}" x2="{width - right}" y1="{y(t):.1f}" y2="{y(t):.1f}" stroke="{GRID}"/>')
        out.append(f'<text x="{left - 6}" y="{y(t) + 4:.1f}" font-size="10" text-anchor="end" fill="{MUTED}" style="{FONT}">{t:+.0f}</text>')
    out.append(f'<line x1="{left}" x2="{width - right}" y1="{y(0):.1f}" y2="{y(0):.1f}" stroke="{INK}" stroke-width="1.2"/>')
    out.append(f'<text x="{width - right}" y="{y(0) - 5:.1f}" font-size="10" text-anchor="end" fill="{MUTED}" style="{FONT}">= headway holding</text>')
    out.append(f'<text x="{left}" y="{top - 6}" font-size="10" fill="{MUTED}" style="{FONT}">seconds per passenger (below zero = better than headway holding)</text>')
    gw = pw / len(groups)
    for gi, g in enumerate(groups):
        for si, s in enumerate(series):
            m, l, h = diffs[g][s]
            cx = left + gi * gw + gw / 2 + (si - (len(series) - 1) / 2) * 18
            c = STRATEGY_COLORS[s]
            out.append(f'<line x1="{cx:.1f}" x2="{cx:.1f}" y1="{y(l):.1f}" y2="{y(h):.1f}" stroke="{c}" stroke-width="2"/>')
            out.append(f'<line x1="{cx - 5:.1f}" x2="{cx + 5:.1f}" y1="{y(l):.1f}" y2="{y(l):.1f}" stroke="{c}" stroke-width="2"/>')
            out.append(f'<line x1="{cx - 5:.1f}" x2="{cx + 5:.1f}" y1="{y(h):.1f}" y2="{y(h):.1f}" stroke="{c}" stroke-width="2"/>')
            out.append(f'<circle cx="{cx:.1f}" cy="{y(m):.1f}" r="4" fill="{c}"/>')
        for li, line in enumerate(g.split("\n")):
            out.append(f'<text x="{left + gi * gw + gw / 2:.1f}" y="{top + ph + 16 + li * 13}" font-size="10.5" text-anchor="middle" fill="{INK}" style="{FONT}">{escape(line)}</text>')
    out.append(legend([(STRATEGY_LABELS[s] + " − headway", STRATEGY_COLORS[s]) for s in series], left, height - 10))
    out.append("</svg>")
    return "".join(out)


def line_chart(series, title="", xlabel="", ylabel="", width=720, height=280, logx=False):
    """series: [(label, colour, [(x, y), ...])]."""
    import math
    left, right, top, bottom = 56, 16, 24, 58
    pw, ph = width - left - right, height - top - bottom
    xs = [x for _, _, pts in series for x, _ in pts]
    ys = [v for _, _, pts in series for _, v in pts]
    fx = (lambda v: math.log10(v)) if logx else (lambda v: v)
    x0, x1 = fx(min(xs)), fx(max(xs))
    ymax = _nice_max(max(ys) * 1.08)
    X = lambda v: left + (fx(v) - x0) / ((x1 - x0) or 1) * pw
    Y = lambda v: top + ph - v / ymax * ph
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="100%" role="img" aria-label="{escape(title)}">']
    for t in _ticks(ymax):
        out.append(f'<line x1="{left}" x2="{width - right}" y1="{Y(t):.1f}" y2="{Y(t):.1f}" stroke="{GRID}"/>')
        out.append(f'<text x="{left - 6}" y="{Y(t) + 4:.1f}" font-size="10" text-anchor="end" fill="{MUTED}" style="{FONT}">{_fmt(t)}</text>')
    for xv in sorted(set(xs)):
        out.append(f'<text x="{X(xv):.1f}" y="{top + ph + 15}" font-size="10" text-anchor="middle" fill="{MUTED}" style="{FONT}">{_fmt(xv)}</text>')
    out.append(f'<text x="{left + pw / 2:.1f}" y="{top + ph + 32}" font-size="10.5" text-anchor="middle" fill="{INK}" style="{FONT}">{escape(xlabel)}</text>')
    out.append(f'<text x="{left}" y="{top - 8}" font-size="10.5" fill="{MUTED}" style="{FONT}">{escape(ylabel)}</text>')
    for label, color, pts in series:
        d = " ".join(f"{'M' if i == 0 else 'L'}{X(x):.1f},{Y(v):.1f}" for i, (x, v) in enumerate(pts))
        out.append(f'<path d="{d}" fill="none" stroke="{color}" stroke-width="2.2"/>')
        for x, v in pts:
            out.append(f'<circle cx="{X(x):.1f}" cy="{Y(v):.1f}" r="3.2" fill="{color}"/>')
    out.append(legend([(l, c) for l, c, _ in series], left, height - 8))
    out.append("</svg>")
    return "".join(out)


def timeline(samples, expected, title="", width=340, height=120, fault_at=30, recover_at=None):
    """Positions delivered to the map per second, with the fault window shaded. Bursts above 3× the expected rate
    (a backlog being delivered after recovery) are clipped and labelled with their peak."""
    left, right, top, bottom = 34, 6, 18, 20
    pw, ph = width - left - right, height - top - bottom
    n = max(s["second"] for s in samples) + 1
    peak = max(s["received"] for s in samples)
    ymax = _nice_max(min(max(peak, expected) * 1.1, expected * 3))
    X = lambda t: left + t / n * pw
    Y = lambda v: top + ph - v / ymax * ph
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="100%" role="img" aria-label="{escape(title)}">']
    out.append(f'<text x="{left}" y="11" font-size="10.5" fill="{INK}" style="{FONT};font-weight:600">{escape(title)}</text>')
    end = recover_at if recover_at is not None else fault_at + 1
    out.append(f'<rect x="{X(fault_at):.1f}" y="{top}" width="{max(2, X(end) - X(fault_at)):.1f}" height="{ph}" fill="#fde2e2"/>')
    out.append(f'<line x1="{left}" x2="{width - right}" y1="{Y(expected):.1f}" y2="{Y(expected):.1f}" stroke="{MUTED}" stroke-dasharray="3 3"/>')
    for t in (0, ymax):
        out.append(f'<text x="{left - 4}" y="{Y(t) + 3:.1f}" font-size="9" text-anchor="end" fill="{MUTED}" style="{FONT}">{_fmt(t)}</text>')
    pts = sorted(samples, key=lambda s: s["second"])
    clip = lambda v: min(v, ymax)
    d = " ".join(f"{'M' if i == 0 else 'L'}{X(s['second']):.1f},{Y(clip(s['received'])):.1f}" for i, s in enumerate(pts))
    out.append(f'<path d="{d}" fill="none" stroke="#2563eb" stroke-width="1.6"/>')
    if peak > ymax:
        ps = max(pts, key=lambda s: s["received"])
        out.append(f'<text x="{X(ps["second"]) + 4:.1f}" y="{top + 9}" font-size="9" fill="{INK}" style="{FONT}">peak {_fmt(peak)}/s (backlog)</text>')
    for t in range(0, n, 30):
        out.append(f'<text x="{X(t):.1f}" y="{height - 6}" font-size="9" text-anchor="middle" fill="{MUTED}" style="{FONT}">{t} s</text>')
    out.append("</svg>")
    return "".join(out)
