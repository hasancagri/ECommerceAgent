#!/usr/bin/env python3
"""İLKE VII görsel katman (Miro): BC FLOW.md'lerini (EventStorming notasyonu) Miro Canvas
Composer SVG'sine çevirir — flow-wall.html'in Miro ikizi. Kullanım:
    python3 scripts/build-miro-wall.py
Çıktı: miro-wall.svg (git'e girmez, .gitignore'da). Boarda basma Claude Miro MCP'siyle yapılır
(canvas_create_from_svg; tek çağrı limiti 200k karakter). Board = üretilmiş salt-okunur katman;
gerçek-kaynak FLOW.md'dir — süreç değişince script yeniden çalıştırılıp board yeniden basılır.

Yüzey dili İngilizce (başlık/lejant/panel başlıkları/sticky adları), anlatılar + panel içerikleri
Türkçe. Sticky çevirileri scripts/miro-sticky-tr-en.json'dan; eksik ad Türkçe kalır + uyarı basar
(yeni sticky ekleyince haritaya çeviri ekle)."""
import re, html, glob, os, json

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "miro-wall.svg")
TR_EN = json.load(open(os.path.join(REPO, "scripts", "miro-sticky-tr-en.json"), encoding="utf-8"))

# Miro sticky adlandırılmış renkleri (hex kabul etmez)
TYPES = {
    "🟦": ("Command", "blue"),
    "🟧": ("Event", "orange"),
    "🟪": ("Policy", "violet"),
    "🟨": ("Aggregate", "yellow"),
    "🟩": ("Read model", "green"),
    "🩷": ("External system", "light_pink"),
    "🟥": ("Hotspot", "red"),
    "👤": ("Actor", "light_yellow"),
}
LEGEND_FILL = {"blue": "#c6dcff", "orange": "#f8d3af", "violet": "#dedaff", "yellow": "#fff6b6",
               "green": "#adf0c7", "light_pink": "#ffc6c6", "red": "#ff6464", "light_yellow": "#fffbed"}
EMOJI_RE = "|".join(TYPES.keys())
TOKEN_RE = r"[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*"

# Kulvar sırası ≈ ürün yaşam akışı (build-flow-wall.py ile aynı)
ORDER = ["catalog", "file", "stock", "discount", "storefront", "basket", "order",
         "payment", "checkout", "customer", "reviews", "library", "NotificationAgent"]
TARGET_MAP = {o.lower(): o for o in ORDER}
TARGET_MAP.update({"notification": "NotificationAgent", "file.api": "file",
                   "checkout.orchestrator": "checkout"})

# Geometri
SW, SH = 199, 228      # sticky kutusu
SGAP = 24              # adım içi sticky aralığı
SLOT_MIN = 380         # adım slotu min genişlik (anlatı taşmasın)
STEP_GAP = 100         # adımlar arası
PAD = 48               # frame iç boşluk
LANE_H = 460
LANE_GAP = 160
TITLE_H = 260          # başlık + lejant alanı
PANEL_W = 560          # kulvar solu bilgi paneli

def lane_id(path):
    for name in ORDER:
        if f"/{name}/" in path: return name
    return os.path.basename(os.path.dirname(path))

def clean_md(s):
    s = re.sub(rf"({EMOJI_RE})|[⭐⇨⇦]", "", s)
    s = s.replace("`", "")
    s = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", s)
    return re.sub(r"\s+", " ", s).strip()

def section_items(text, header_re):
    m = re.search(rf"## {header_re}[^\n]*\n(.*?)(?=\n## |\Z)", text, re.S)
    if not m: return []
    return [clean_md(line.strip()[2:]) for line in re.split(r"\n(?=- )", m.group(1).strip())
            if line.strip().startswith("- ")]

def parse_flow(path):
    text = open(path, encoding="utf-8").read()
    title_m = re.search(r"^# (.+?) — Domain Süreci", text, re.M)
    bc = title_m.group(1) if title_m else lane_id(path)
    purpose_m = re.search(r"\*\*BC ne yapar:\*\* (.+?)(?:\n\n|\n>)", text, re.S)
    purpose = clean_md(purpose_m.group(1)) if purpose_m else ""
    proc_m = re.search(r"## Süreç\n(.*?)(?=\n## |\Z)", text, re.S)
    steps = []
    if proc_m:
        for raw in re.split(r"\n(?=\s{0,1}\d+[a-z]?\.\s)", proc_m.group(1)):
            raw = raw.strip()
            if not re.match(r"^\d+[a-z]?\.", raw): continue
            num = re.match(r"^(\d+[a-z]?)\.", raw).group(1)
            flat = re.sub(r"\s+", " ", raw)
            anchors = " ".join(a for a in re.findall(r"`([^`]+)`", flat) if re.search(r"[A-Z]", a))
            tokens = set(re.findall(TOKEN_RE, anchors))
            body = re.sub(r"`[^`]+`", "", flat)
            stickies = [(m.group(1), bool(m.group(2)), m.group(3))
                        for m in re.finditer(rf"({EMOJI_RE})(⭐)?\s*\*\*(.+?)\*\*", body)]
            nar = ""
            nm = re.search(r" — (.+)$", body)
            if nm: nar = re.sub(rf"({EMOJI_RE})|\*\*", "", nm.group(1)).strip()
            seams = []
            for d, t in re.findall(r"(⇨|⇦)\s+([A-ZÇĞİÖŞÜ][\w.]*(?:,\s*[A-ZÇĞİÖŞÜ][\w.]*)*)", body):
                seams.extend((d, one.strip()) for one in t.split(","))
            if stickies:
                steps.append(dict(num=num, stickies=stickies, nar=nar, seams=seams, tokens=tokens))
    return dict(id=lane_id(path), bc=bc, purpose=purpose, steps=steps,
                rules=section_items(text, r"Domain kuralları"), boundary=section_items(text, r"Sınır"))

files = sorted(glob.glob(f"{REPO}/src/**/FLOW.md", recursive=True),
               key=lambda p: ORDER.index(lane_id(p)) if lane_id(p) in ORDER else 99)
lanes = [parse_flow(f) for f in files]
by_id = {l["id"]: l for l in lanes}

# --- Dikiş -> ok eşleştirme (build-flow-wall.py ile aynı mantık) ---
def resolve_lane(name): return TARGET_MAP.get(name.lower())

def find_step(dst_lane, src_step, opp_dir, counterparty_id):
    best, best_len = None, 0
    for st in dst_lane["steps"]:
        shared = src_step["tokens"] & st["tokens"]
        if not shared: continue
        ln = max(len(t) for t in shared)
        if ln >= 8 and ln > best_len:
            best, best_len = st, ln
    if best: return best, True
    for st in dst_lane["steps"]:
        if any(d == opp_dir and resolve_lane(t) == counterparty_id for d, t in st["seams"]):
            return st, False
    return None, False

edges, seen = [], set()
for lane in lanes:
    for step in lane["steps"]:
        here = f'st_{lane["id"]}_{step["num"]}'
        for d, target in step["seams"]:
            dst_id = resolve_lane(target)
            if not dst_id or dst_id == lane["id"]: continue
            m, by_token = find_step(by_id[dst_id], step, "⇦" if d == "⇨" else "⇨", lane["id"])
            if not m:
                m = by_id[dst_id]["steps"][0] if by_id[dst_id]["steps"] else None
                if not m: continue
            there = f'st_{dst_id}_{m["num"]}'
            src, dst = (here, there) if d == "⇨" else (there, here)
            if (src, dst) in seen: continue
            seen.add((src, dst))
            tok = max(step["tokens"] & m["tokens"], key=len, default="") if by_token else ""
            edges.append(dict(s=src, t=dst, k=tok))

def esc(s): return html.escape(s, quote=True)

def xml_text(s):
    """textArea gövdesi: <b>/<br/> kalır, gerisi escape."""
    s = html.escape(s)
    return s.replace("&lt;b&gt;", "<b>").replace("&lt;/b&gt;", "</b>").replace("&lt;br/&gt;", "<br/>")

missing = set()
def sticky_en(name):
    plain = re.sub(r"\s+", " ", name).strip()
    en = TR_EN.get(plain)
    if en is None:
        missing.add(plain)
        return plain
    return en

# --- SVG üretimi ---
sticky_pos = {}
frames_svg, panels_svg = [], []
y = TITLE_H
total_stickies = 0
for lane in lanes:
    inner = []
    x = PAD
    for step in lane["steps"]:
        sid0 = f'st_{lane["id"]}_{step["num"]}'
        n = len(step["stickies"])
        step_w = n * SW + (n - 1) * SGAP
        slot = max(step_w, SLOT_MIN)
        for i, (emoji, piv, name) in enumerate(step["stickies"]):
            _, color = TYPES[emoji]
            sx = x + i * (SW + SGAP)
            content = esc(sticky_en(name))
            if piv: content = f"&lt;b&gt;{content}&lt;/b&gt;"
            lid = f' id="{sid0}"' if i == 0 else ""
            inner.append(f'<rect{lid} data-type="sticky" x="{sx}" y="{PAD}" width="{SW}" height="{SH}" data-color="{color}" data-content="{content}" />')
            if i == 0: sticky_pos[sid0] = (sx, y + PAD)
            total_stickies += 1
        nar = f'{step["num"]}. {step["nar"]}' if step["nar"] else f'Adım {step["num"]}'
        if len(nar) > 220: nar = nar[:217] + "..."
        inner.append(f'<textArea x="{x}" y="{PAD + SH + 16}" width="{slot}" font-size="14" fill="#595959">{esc(nar)}</textArea>')
        x += slot + STEP_GAP
    w = max(x - STEP_GAP + PAD, 900)
    frames_svg.append(
        f'<g id="ln_{lane["id"]}" transform="translate(0,{y})" data-frame="{esc(lane["bc"])}">\n'
        f'<rect data-type="frame" x="0" y="0" width="{w}" height="{LANE_H}" fill="#f7f7f7" />\n'
        + "\n".join(inner) + "\n</g>")
    # kulvar solu bilgi paneli (başlıklar EN, içerik TR)
    parts = [f"<b>Purpose</b><br/>{lane['purpose']}"]
    if lane["rules"]:
        parts.append("<b>Domain rules</b><br/>" + "<br/>".join("- " + r for r in lane["rules"]))
    if lane["boundary"]:
        parts.append("<b>Boundary</b><br/>" + "<br/>".join("- " + b for b in lane["boundary"]))
    body = "<br/><br/>".join(parts)
    if len(body) > 2800: body = body[:2797] + "..."
    panels_svg.append(f'<textArea x="{-PANEL_W - 60}" y="{y + 24}" width="{PANEL_W}" font-size="12" fill="#595959" font-family="noto_sans">{xml_text(body)}</textArea>')
    y += LANE_H + LANE_GAP

head = ['<text x="0" y="60" font-size="67" font-weight="bold" fill="#1a1a1a" font-family="noto_sans">EventStorming Wall - ECommerceAgent</text>',
        f'<text x="0" y="110" font-size="22" fill="#595959" font-family="noto_sans">{len(lanes)} BC lanes - {total_stickies} stickies - {len(edges)} seam arrows - source: each BC\'s FLOW.md (read-only, generated)</text>']
lx = 0
for emoji, (typ, color) in TYPES.items():
    head.append(f'<rect x="{lx}" y="150" width="170" height="44" rx="6" fill="{LEGEND_FILL[color]}" stroke="none" data-content="{esc(typ)}" data-text-color="#1a1a1a" data-font-size="14" />')
    lx += 190

conns = []
for i, e in enumerate(edges):
    sy = sticky_pos.get(e["s"]); ty = sticky_pos.get(e["t"])
    if not sy or not ty: continue
    down = ty[1] > sy[1]
    ss, es = ("bottom", "top") if down else ("top", "bottom")
    label = f' data-content="{esc(e["k"])}"' if e["k"] else ""
    conns.append(f'<line id="e{i}" x1="{sy[0]+SW//2}" y1="{sy[1]}" x2="{ty[0]+SW//2}" y2="{ty[1]}" stroke="#313131" stroke-width="2" data-arrow="end" data-shape="curved" data-start="{e["s"]}" data-end="{e["t"]}" data-start-side="{ss}" data-end-side="{es}"{label} />')

full = "<svg>\n" + "\n".join(head) + "\n" + "\n".join(conns) + "\n" + "\n".join(frames_svg) + "\n" + "\n".join(panels_svg) + "\n</svg>"
open(OUT, "w", encoding="utf-8").write(full)
for m in sorted(missing):
    print(f"UYARI: çeviri yok (TR kaldı): {m!r} — scripts/miro-sticky-tr-en.json'a ekle")
print(f"OK {os.path.relpath(OUT, REPO)} — {len(lanes)} kulvar, {total_stickies} sticky, "
      f"{len(edges)} dikiş, {len(full)} karakter{' — UYARILI' if missing else ''}")
