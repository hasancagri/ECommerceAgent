#!/usr/bin/env python3
"""İLKE VII görsel katman: BC FLOW.md'lerini (EventStorming notasyonu) parse edip tüm sistemi
tek duvarda gösteren salt-okunur flow-wall.html üretir. Kullanım:
    python3 scripts/build-flow-wall.py && open flow-wall.html
Çıktı üretilebilir dosyadır (git'e girmez, .gitignore'da). Dikişler (⇨/⇦) kulvarlar arası
gerçek ok olarak çizilir: eşleştirme ortak anchor token'ından (yayıncı adımı → tüketici adımı).
"""
import re, html, glob, os, json

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "flow-wall.html")

TYPES = {
    "🟦": ("komut", "#5B8DEF"),
    "🟧": ("event", "#F6A13C"),
    "🟪": ("policy", "#B07CD8"),
    "🟨": ("aggregate", "#F2D04B"),
    "🟩": ("read-model", "#7FC97F"),
    "🩷": ("dış sistem", "#F2A0C0"),
    "🟥": ("hotspot", "#E4572E"),
    "👤": ("aktör", "#E8D9A0"),
}
EMOJI_RE = "|".join(TYPES.keys())
TOKEN_RE = r"[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*"

# Kulvar sırası ≈ ürün yaşam akışı. Dizin adı = kulvar kimliği.
ORDER = ["catalog", "file", "stock", "discount", "storefront", "basket", "order",
         "payment", "checkout", "customer", "reviews", "library", "NotificationAgent"]
# Dikiş hedef adı (FLOW.md'de yazılan) → kulvar kimliği. Bilinmeyen (PG, Mailpit…) ok çizmez.
TARGET_MAP = {o.lower(): o for o in ORDER}
TARGET_MAP.update({"notification": "NotificationAgent", "file.api": "file",
                   "checkout.orchestrator": "checkout"})

def lane_id(path):
    for name in ORDER:
        if f"/{name}/" in path: return name
    return os.path.basename(os.path.dirname(path))

def parse_flow(path):
    text = open(path, encoding="utf-8").read()
    title_m = re.search(r"^# (.+?) — Domain Süreci", text, re.M)
    bc = title_m.group(1) if title_m else lane_id(path)
    purpose_m = re.search(r"\*\*BC ne yapar:\*\* (.+?)(?:\n\n|\n>)", text, re.S)
    purpose = re.sub(r"\s+", " ", purpose_m.group(1)) if purpose_m else ""
    purpose = re.sub(r"[*`]", "", purpose)
    if len(purpose) > 220: purpose = purpose[:217] + "…"

    proc_m = re.search(r"## Süreç\n(.*?)(?=\n## |\Z)", text, re.S)
    steps = []
    if proc_m:
        raw_steps = re.split(r"\n(?=\s{0,1}\d+[a-z]?\.\s)", proc_m.group(1))
        for raw in raw_steps:
            raw = raw.strip()
            if not re.match(r"^\d+[a-z]?\.", raw):
                continue
            num = re.match(r"^(\d+[a-z]?)\.", raw).group(1)
            flat = re.sub(r"\s+", " ", raw)
            anchors = " · ".join(a.strip("() ") for a in re.findall(r"`([^`]+)`", flat)
                                 if re.search(r"[A-Z]", a))
            tokens = set(re.findall(TOKEN_RE, anchors))
            body = re.sub(r"`[^`]+`", "", flat)
            stickies = [(m.group(1), bool(m.group(2)), m.group(3))
                        for m in re.finditer(rf"({EMOJI_RE})(⭐)?\s*\*\*(.+?)\*\*", body)]
            nar = ""
            nm = re.search(r" — (.+)$", body)
            if nm:
                nar = re.sub(rf"({EMOJI_RE})|\*\*", "", nm.group(1)).strip()
            seams = []
            for d, t in re.findall(r"(⇨|⇦)\s+([A-ZÇĞİÖŞÜ][\w.]*(?:,\s*[A-ZÇĞİÖŞÜ][\w.]*)*)", body):
                for one in t.split(","):
                    seams.append((d, one.strip()))
            if stickies:
                steps.append(dict(num=num, stickies=stickies, nar=nar, seams=seams,
                                  anchors=anchors, tokens=tokens))
    return dict(id=lane_id(path), bc=bc, purpose=purpose, steps=steps)

files = sorted(glob.glob(f"{REPO}/src/**/FLOW.md", recursive=True),
               key=lambda p: ORDER.index(lane_id(p)) if lane_id(p) in ORDER else 99)
lanes = [parse_flow(f) for f in files]
by_id = {l["id"]: l for l in lanes}

# --- Dikiş -> ok eşleştirme (Python'da; JS yalnız geometri çizer) ---
def resolve_lane(name):
    return TARGET_MAP.get(name.lower())

def find_step(dst_lane, src_step, opp_dir, counterparty_id):
    """Karşı kulvarda uç adımı bul: (1) ≥8 harf ortak anchor token'ı (en uzunu kazanır),
    (2) yoksa karşı-yönlü dikişi bu kulvarı gösteren İLK adım (deterministik), (3) yoksa None."""
    best, best_len = None, 0
    for st in dst_lane["steps"]:
        shared = src_step["tokens"] & st["tokens"]
        if not shared: continue
        ln = max(len(t) for t in shared)
        if ln >= 8 and ln > best_len:  # "Order" gibi jenerik token'a ok bağlama
            best, best_len = st, ln
    if best: return best, True
    for st in dst_lane["steps"]:
        if any(d == opp_dir and resolve_lane(t) == counterparty_id for d, t in st["seams"]):
            return st, False
    return None, False

edges, seen = [], set()
for lane in lanes:
    for step in lane["steps"]:
        here = f'st-{lane["id"]}-{step["num"]}'
        for d, target in step["seams"]:
            dst_id = resolve_lane(target)
            if not dst_id or dst_id == lane["id"]: continue
            m, by_token = find_step(by_id[dst_id], step, "⇦" if d == "⇨" else "⇨", lane["id"])
            there = f'st-{dst_id}-{m["num"]}' if m else f'ln-{dst_id}'
            src, dst = (here, there) if d == "⇨" else (there, here)
            if (src, dst) in seen: continue  # iki uç da ilan edince tek ok
            seen.add((src, dst))
            tok = max(step["tokens"] & m["tokens"], key=len, default="") if (m and by_token) else ""
            edges.append(dict(s=src, t=dst, k=tok))

def sticky_html(emoji, pivotal, name, idx):
    typ, color = TYPES[emoji]
    cls = "sticky hot" if typ == "hotspot" else "sticky"
    star = '<span class="star">⭐</span>' if pivotal else ""
    rot = [-1.6, 1.2, -0.7, 1.8, -1.2, 0.9][idx % 6]
    return (f'<div class="{cls}" style="--c:{color};--r:{rot}deg">'
            f'<span class="typ">{typ}</span>{star}<span class="nm">{html.escape(name)}</span></div>')

body = []
total_stickies, total_stars = 0, 0
for lane in lanes:
    cards = []
    for step in lane["steps"]:
        st_html = '<span class="arr">→</span>'.join(
            sticky_html(e, p, n, i) for i, (e, p, n) in enumerate(step["stickies"]))
        total_stickies += len(step["stickies"])
        total_stars += sum(1 for _, p, _ in step["stickies"] if p)
        seam_html = "".join(f'<span class="seam {"out" if d=="⇨" else "inn"}">{d} {html.escape(t)}</span>'
                            for d, t in step["seams"])
        nar = html.escape(step["nar"][:260])
        anch = html.escape(step["anchors"])
        cards.append(f'''<div class="step" id="st-{lane["id"]}-{step["num"]}">
  <div class="stephead"><span class="num">{step["num"]}</span>{seam_html}</div>
  <div class="chain">{st_html}</div>
  <p class="nar">{nar}</p>
  {f'<p class="anch">{anch}</p>' if anch else ""}
</div>''')
    body.append(f'''<section class="lane" id="ln-{lane["id"]}">
  <header class="lanehead"><h2>{html.escape(lane["bc"])}</h2><p>{html.escape(lane["purpose"])}</p></header>
  <div class="steps">{"".join(cards)}</div>
</section>''')

legend = "".join(f'<span class="lg" style="--c:{c}">{e} {t}</span>' for e, (t, c) in TYPES.items())

page = f'''<!doctype html>
<html lang="tr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>EventStorming Duvarı — ECommerceAgent</title>
<style>
  :root {{ --wall:#EEEBE3; --ink:#2B2822; --mut:#6E675C; --line:#D8D2C4; --wire:#A35B2E; }}
  body {{ background:var(--wall); color:var(--ink); margin:0;
         font:15px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif; }}
  .top {{ padding:28px 32px 18px; border-bottom:2px solid var(--line);
          background:linear-gradient(#F4F1E9,var(--wall)); }}
  .top h1 {{ margin:0 0 4px; font-size:26px; letter-spacing:-.3px; text-wrap:balance; }}
  .top .sub {{ margin:0; color:var(--mut); font-size:14px; }}
  .legend {{ display:flex; flex-wrap:wrap; gap:8px 14px; margin-top:12px; font-size:12.5px; align-items:center; }}
  .lg {{ display:inline-flex; align-items:center; gap:4px; padding:2px 9px; border-radius:3px;
         background:color-mix(in srgb, var(--c) 22%, white); border:1px solid color-mix(in srgb, var(--c) 45%, white); }}
  .legend button {{ margin-left:auto; font:inherit; font-size:12.5px; padding:3px 12px; cursor:pointer;
         border:1px solid #9A917F; border-radius:3px; background:#F7F5EE; color:var(--ink); }}
  .wall {{ overflow:auto; }}
  .content {{ position:relative; width:max-content; min-width:100%; }}
  .wires {{ position:absolute; inset:0; pointer-events:none; z-index:3; }}
  .wires path {{ fill:none; stroke:var(--wire); stroke-width:1.4; opacity:.38; }}
  .wires path.on {{ opacity:1; stroke-width:2.2; }}
  .wires.off {{ display:none; }}
  .lane {{ border-bottom:2px solid var(--line); }}
  .lanehead {{ padding:14px 32px 2px; display:flex; align-items:baseline; gap:14px; flex-wrap:wrap;
               position:sticky; left:0; width:min(92vw, 1100px); box-sizing:border-box; }}
  .lanehead h2 {{ margin:0; font-size:17px; text-transform:uppercase; letter-spacing:.08em; }}
  .lanehead p {{ margin:0; color:var(--mut); font-size:12.5px; max-width:70ch; }}
  .steps {{ display:flex; gap:14px; padding:12px 32px 20px; align-items:flex-start; width:max-content; }}
  .step {{ flex:0 0 auto; width:290px; background:#F7F5EE; border:1px solid var(--line);
           border-radius:4px; padding:10px 12px 8px; position:relative; z-index:1; }}
  .step.on {{ border-color:var(--wire); box-shadow:0 0 0 1px var(--wire); }}
  .stephead {{ display:flex; gap:6px; align-items:center; flex-wrap:wrap; margin-bottom:8px; }}
  .num {{ font-weight:700; font-size:12px; color:var(--mut); background:#E7E2D5;
          border-radius:3px; padding:1px 7px; font-variant-numeric:tabular-nums; }}
  .seam {{ font-size:11px; padding:1px 7px; border-radius:10px; border:1px dashed #9A917F; color:#5A5344; }}
  .seam.out {{ background:#E9F0E4; }} .seam.inn {{ background:#EAE6F2; }}
  .chain {{ display:flex; flex-wrap:wrap; gap:4px; align-items:center; }}
  .arr {{ color:var(--mut); font-size:13px; }}
  .sticky {{ --r:0deg; background:var(--c); color:#2A2419; padding:7px 9px 8px; width:116px;
             min-height:52px; box-shadow:1.5px 2.5px 4px rgba(60,50,30,.28); transform:rotate(var(--r));
             display:flex; flex-direction:column; gap:2px; }}
  .sticky .typ {{ font-size:9.5px; text-transform:uppercase; letter-spacing:.1em; opacity:.62; }}
  .sticky .nm {{ font-size:12.5px; font-weight:650; line-height:1.22; text-wrap:balance; }}
  .sticky .star {{ position:absolute; margin-left:92px; margin-top:-2px; font-size:13px; }}
  .sticky.hot {{ color:#fff; transform:rotate(calc(var(--r) + 4deg)); }}
  .sticky.hot .typ {{ opacity:.85; }}
  .nar {{ margin:9px 0 0; font-size:12px; color:#4C4538; }}
  .anch {{ margin:6px 0 0; font-size:10.5px; color:var(--mut);
           font-family:ui-monospace, "SF Mono", Menlo, monospace; word-break:break-word; }}
  .foot {{ padding:16px 32px 28px; color:var(--mut); font-size:12.5px; }}
  @media (prefers-reduced-motion: no-preference) {{
    .sticky {{ transition:transform .15s; }} .sticky:hover {{ transform:rotate(0) scale(1.04); }} }}
</style></head><body>
<div class="top">
  <h1>EventStorming Duvarı — ECommerceAgent</h1>
  <p class="sub">{len(lanes)} BC kulvarı · {total_stickies} sticky · {len(edges)} dikiş oku · {total_stars} pivotal —
     kaynak: her BC'nin FLOW.md'si (salt-okunur; üretim: scripts/build-flow-wall.py)</p>
  <div class="legend">{legend} <span class="lg" style="--c:#9A917F">⇨/⇦ dikiş rozeti</span>
    <span class="lg" style="--c:var(--wire)">╰→ dikiş oku (karta gelince vurgulanır)</span>
    <button id="wiretoggle" type="button">Okları gizle</button></div>
</div>
<div class="wall"><div class="content">
<svg class="wires" id="wires" aria-hidden="true">
  <defs><marker id="ah" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto">
    <path d="M0,0 L8,4 L0,8 z" fill="var(--wire)"/></marker></defs>
</svg>
{"".join(body)}
</div></div>
<p class="foot">Kulvar içinde zaman soldan sağa akar; kart = FLOW.md süreç adımı (sticky zinciri + anlatı + kod-anchor).
Oklar BC-arası dikişlerdir: yayıncı adımından tüketici adımına (eşleşme ortak kod-anchor'ından). Karta gelince o kartın okları vurgulanır.</p>
<script>
const EDGES = {json.dumps(edges, ensure_ascii=False)};
const svg = document.getElementById("wires");
const content = document.querySelector(".content");
const byStep = new Map();
function draw() {{
  for (const p of svg.querySelectorAll("path.wire")) p.remove();
  byStep.clear();
  const c = content.getBoundingClientRect();
  svg.setAttribute("width", content.scrollWidth); svg.setAttribute("height", content.scrollHeight);
  for (const e of EDGES) {{
    const s = document.getElementById(e.s), t = document.getElementById(e.t);
    if (!s || !t) continue;
    const rs = s.getBoundingClientRect(), rt = t.getBoundingClientRect();
    const down = rt.top > rs.top;
    const x1 = rs.left - c.left + rs.width / 2, y1 = (down ? rs.bottom : rs.top) - c.top;
    const x2 = rt.left - c.left + rt.width / 2, y2 = (down ? rt.top : rt.bottom) - c.top;
    const bend = Math.max(36, Math.abs(y2 - y1) * 0.35) * (down ? 1 : -1);
    const p = document.createElementNS("http://www.w3.org/2000/svg", "path");
    p.setAttribute("d", `M${{x1}},${{y1}} C${{x1}},${{y1 + bend}} ${{x2}},${{y2 - bend}} ${{x2}},${{y2}}`);
    p.setAttribute("class", "wire"); p.setAttribute("marker-end", "url(#ah)");
    p.dataset.k = e.k; svg.appendChild(p);
    for (const id of [e.s, e.t]) {{
      if (!byStep.has(id)) byStep.set(id, []);
      byStep.get(id).push(p);
    }}
  }}
}}
draw();
let rt; addEventListener("resize", () => {{ clearTimeout(rt); rt = setTimeout(draw, 120); }});
for (const el of document.querySelectorAll(".step")) {{
  el.addEventListener("mouseenter", () => {{
    el.classList.add("on");
    for (const p of byStep.get(el.id) || []) p.classList.add("on");
  }});
  el.addEventListener("mouseleave", () => {{
    el.classList.remove("on");
    for (const p of byStep.get(el.id) || []) p.classList.remove("on");
  }});
}}
document.getElementById("wiretoggle").addEventListener("click", (ev) => {{
  const off = svg.classList.toggle("off");
  ev.target.textContent = off ? "Okları göster" : "Okları gizle";
}});
</script>
</body></html>
'''
open(OUT, "w", encoding="utf-8").write(page)
print(f"OK {os.path.relpath(OUT, REPO)} — {len(lanes)} kulvar, {total_stickies} sticky, "
      f"{len(edges)} dikiş oku, {total_stars} pivotal")
