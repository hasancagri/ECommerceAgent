#!/usr/bin/env python3
"""İLKE VII görsel katman: BC FLOW.md'lerini (EventStorming notasyonu) parse edip tüm sistemi
tek duvarda gösteren salt-okunur flow-wall.html üretir. Kullanım:
    python3 scripts/build-flow-wall.py && open flow-wall.html
Çıktı üretilebilir dosyadır (git'e girmez, .gitignore'da)."""
import re, html, glob, os

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

def parse_flow(path):
    text = open(path, encoding="utf-8").read()
    title_m = re.search(r"^# (.+?) — Domain Süreci", text, re.M)
    bc = title_m.group(1) if title_m else os.path.basename(os.path.dirname(path))
    purpose_m = re.search(r"\*\*BC ne yapar:\*\* (.+?)(?:\n\n|\n>)", text, re.S)
    purpose = re.sub(r"\s+", " ", purpose_m.group(1)) if purpose_m else ""
    purpose = re.sub(r"[*`]", "", purpose)
    if len(purpose) > 220: purpose = purpose[:217] + "…"

    proc_m = re.search(r"## Süreç\n(.*?)(?=\n## |\Z)", text, re.S)
    steps = []
    if proc_m:
        block = proc_m.group(1)
        raw_steps = re.split(r"\n(?=\s{0,1}\d+[a-z]?\.\s)", block)
        for raw in raw_steps:
            raw = raw.strip()
            if not re.match(r"^\d+[a-z]?\.", raw):
                continue
            num = re.match(r"^(\d+[a-z]?)\.", raw).group(1)
            flat = re.sub(r"\s+", " ", raw)
            anchors = " · ".join(a.strip("() ") for a in re.findall(r"`([^`]+)`", flat)
                                 if re.search(r"[A-Z]", a))
            body = re.sub(r"`[^`]+`", "", flat)
            stickies = []
            for m in re.finditer(rf"({EMOJI_RE})(⭐)?\s*\*\*(.+?)\*\*", body):
                stickies.append((m.group(1), bool(m.group(2)), m.group(3)))
            nar = ""
            nm = re.search(r" — (.+)$", body)
            if nm:
                nar = re.sub(rf"({EMOJI_RE})|\*\*", "", nm.group(1)).strip()
            seams = [(d, t.strip()) for d, t in
                     re.findall(r"(⇨|⇦)\s+([A-ZÇĞİÖŞÜ][\w.]*(?:,\s*[A-ZÇĞİÖŞÜ][\w.]*)*)", body)]
            if stickies:
                steps.append(dict(num=num, stickies=stickies, nar=nar, seams=seams, anchors=anchors))
    return dict(bc=bc, purpose=purpose, steps=steps)

# Kulvar sırası ≈ ürün yaşam akışı: katalog girişinden satın-alma-sonrasına.
ORDER = ["catalog", "file", "stock", "discount", "storefront", "basket", "order",
         "payment", "checkout", "customer", "reviews", "library", "NotificationAgent"]

def lane_key(path):
    for i, name in enumerate(ORDER):
        if f"/{name}/" in path: return i
    return 99

files = sorted(glob.glob(f"{REPO}/src/**/FLOW.md", recursive=True), key=lane_key)
lanes = [parse_flow(f) for f in files]

def sticky_html(emoji, pivotal, name, idx):
    typ, color = TYPES[emoji]
    cls = "sticky hot" if typ == "hotspot" else "sticky"
    star = '<span class="star">⭐</span>' if pivotal else ""
    rot = [-1.6, 1.2, -0.7, 1.8, -1.2, 0.9][idx % 6]
    return (f'<div class="{cls}" style="--c:{color};--r:{rot}deg">'
            f'<span class="typ">{typ}</span>{star}<span class="nm">{html.escape(name)}</span></div>')

body = []
total_stickies, total_seams, total_stars = 0, 0, 0
for lane in lanes:
    cards = []
    for step in lane["steps"]:
        st_html = '<span class="arr">→</span>'.join(
            sticky_html(e, p, n, i) for i, (e, p, n) in enumerate(step["stickies"]))
        total_stickies += len(step["stickies"])
        total_stars += sum(1 for _, p, _ in step["stickies"] if p)
        seam_html = "".join(f'<span class="seam {"out" if d=="⇨" else "inn"}">{d} {html.escape(t)}</span>'
                            for d, t in step["seams"])
        total_seams += len(step["seams"])
        nar = html.escape(step["nar"][:260])
        anch = html.escape(step["anchors"])
        cards.append(f'''<div class="step">
  <div class="stephead"><span class="num">{step["num"]}</span>{seam_html}</div>
  <div class="chain">{st_html}</div>
  <p class="nar">{nar}</p>
  {f'<p class="anch">{anch}</p>' if anch else ""}
</div>''')
    body.append(f'''<section class="lane">
  <header class="lanehead"><h2>{html.escape(lane["bc"])}</h2><p>{html.escape(lane["purpose"])}</p></header>
  <div class="steps">{"".join(cards)}</div>
</section>''')

legend = "".join(f'<span class="lg" style="--c:{c}">{e} {t}</span>' for e, (t, c) in TYPES.items())

page = f'''<!doctype html>
<html lang="tr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>EventStorming Duvarı — ECommerceAgent</title>
<style>
  :root {{ --wall:#EEEBE3; --ink:#2B2822; --mut:#6E675C; --line:#D8D2C4; }}
  body {{ background:var(--wall); color:var(--ink); margin:0;
         font:15px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif; }}
  .top {{ padding:28px 32px 18px; border-bottom:2px solid var(--line);
          background:linear-gradient(#F4F1E9,var(--wall)); }}
  .top h1 {{ margin:0 0 4px; font-size:26px; letter-spacing:-.3px; text-wrap:balance; }}
  .top .sub {{ margin:0; color:var(--mut); font-size:14px; }}
  .legend {{ display:flex; flex-wrap:wrap; gap:8px 14px; margin-top:12px; font-size:12.5px; }}
  .lg {{ display:inline-flex; align-items:center; gap:4px; padding:2px 9px; border-radius:3px;
         background:color-mix(in srgb, var(--c) 22%, white); border:1px solid color-mix(in srgb, var(--c) 45%, white); }}
  .lane {{ border-bottom:2px solid var(--line); }}
  .lanehead {{ padding:14px 32px 2px; display:flex; align-items:baseline; gap:14px; flex-wrap:wrap; }}
  .lanehead h2 {{ margin:0; font-size:17px; text-transform:uppercase; letter-spacing:.08em; }}
  .lanehead p {{ margin:0; color:var(--mut); font-size:12.5px; max-width:70ch; }}
  .steps {{ display:flex; gap:14px; overflow-x:auto; padding:12px 32px 20px; align-items:flex-start; }}
  .step {{ flex:0 0 auto; width:290px; background:#F7F5EE; border:1px solid var(--line);
           border-radius:4px; padding:10px 12px 8px; }}
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
  <p class="sub">{len(lanes)} BC kulvarı · {total_stickies} sticky · {total_seams} dikiş · {total_stars} pivotal —
     kaynak: her BC'nin FLOW.md'si (salt-okunur; üretim: scripts/build-flow-wall.py)</p>
  <div class="legend">{legend} <span class="lg" style="--c:#9A917F">⇨/⇦ BC-arası dikiş</span></div>
</div>
{"".join(body)}
<p class="foot">Kulvar içinde zaman soldan sağa akar; kart = FLOW.md süreç adımı (sticky zinciri + anlatı + kod-anchor).
Dikiş rozetleri event'in hangi BC'den gelip hangisine gittiğini söyler.</p>
</body></html>
'''
open(OUT, "w", encoding="utf-8").write(page)
print(f"OK {os.path.relpath(OUT, REPO)} — {len(lanes)} kulvar, {total_stickies} sticky, "
      f"{total_seams} dikiş, {total_stars} pivotal")