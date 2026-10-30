#!/usr/bin/env python3
"""Kitapyurdu books.json kategori kanonikleştirme.

Kitapyurdu taksonomisi GRAF (aynı leaf adı çok-parent'ta) + Category modeli NormalizedName
GLOBAL-unique. Bu script ham `categoryPath`/`relatedCategoryPaths`'i, her ad TEK üst'e oturacak
şekilde kanonik 2-seviye ağaca indirger — böylece model dokunulmadan çakışma kalmaz.

Kurallar:
  OTOMATİK (veriden, 24k'ya ölçeklenir):
    - generic leaf (Diğer/Genel) at → ürün bir üst'e (leaf'siz) bağlanır
    - derinlik normalize: 3+ seviye → [üst, son-anlamlı-leaf]
    - çakışan leaf adı → ÇOĞUNLUK üst tutar; azınlık üst'teki kitaplar kendi üstüne (leaf'siz)
    - leaf adı bir ÜST kategori adıysa → leaf'siz (üst'e bağla; leaf==üst karmaşası önlenir)
  ELLE OVERRIDE (category_overrides.json):
    - Çocuk Kitapları tür-kovaları + çöp-üst "Diğer" dağıtımı

Kullanım:
  python3 scripts/book-import/canonicalize_categories.py [books.json] [overrides.json]
Girdi = HAM (importer çıktısı). Çıktı aynı dosyanın üzerine. BİR KEZ çalıştırılır
(idempotent DEĞİL — çift çalıştırma guard'ı var). Pipeline: importer → bu script → commit."""
import json, sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.join(HERE, "..", "..")
BOOKS = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    REPO, "src/services/catalog/Catalog.Api/Seeding/Data/books.json")
OVR = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "category_overrides.json")

with open(OVR, encoding="utf-8") as f:
    O = json.load(f)
GENERIC = {s.lower() for s in O["generic_leaves"]}
CHILD_TOP = O["child_top"]
CHILD_BUCKETS = O["child_buckets"]          # ordered [name, [keywords]]
JUNK_TOPS = {s.lower() for s in O["junk_tops"]}
JUNK_REMAP = O["junk_remap"]
JUNK_DEFAULT = O["junk_default_top"]

with open(BOOKS, encoding="utf-8") as f:
    books = json.load(f)

# idempotency guard: yalnız UYDURMA kova adı (" & " içeren, hiçbir ham leaf'te yok) görülürse
# zaten koşmuş demektir — "Okul Öncesi"/"Roman"/"Şiir" ham adlardır, marker DEĞİL
canon_markers = {name for name, _ in CHILD_BUCKETS if "&" in name}
for b in books:
    if b.get("categoryPath") and len(b["categoryPath"]) == 2 and b["categoryPath"][1] in canon_markers:
        sys.exit(f"HATA: veri zaten kanonik görünüyor (ör. leaf '{b['categoryPath'][1]}'). "
                 f"Çift çalıştırma; ham books.json üzerinde koştur.")

def is_generic(seg):
    return seg.strip().lower() in GENERIC

def child_bucket(rest):
    """Çocuk Kitapları altı leaf-zincirini kanonik kovaya indir; yoksa None (doğrudan)."""
    joined = " > ".join(rest)
    for name, kws in CHILD_BUCKETS:
        if any(k in joined for k in kws):
            return name
    return None

def shape(path):
    """Ham segment listesi → kanonik [üst] ya da [üst, leaf]. None = kategorisiz (olmaz, hep üst var)."""
    if not path:
        return None
    top = path[0]
    rest = path[1:]
    # çöp üst "Diğer" → remap (sonraki segmente göre)
    if top.strip().lower() in JUNK_TOPS:
        key = rest[0] if rest else None
        target = JUNK_REMAP.get(key, [JUNK_DEFAULT])
        return list(target)
    # Çocuk: tür-kovası
    if top == CHILD_TOP:
        if not rest:
            return [top]
        bucket = child_bucket(rest)
        return [top, bucket] if bucket else [top]
    # genel: generic segmentleri at, derinliği 2'ye indir
    rest = [r for r in rest if not is_generic(r)]
    if not rest:
        return [top]
    return [top, rest[-1]]

# 1. geçiş: shape uygula, leaf→üst sayımı (çoğunluk için, primary üzerinden)
shaped_primary = []
leaf_top_count = {}
tops = set()
for b in books:
    s = shape(b.get("categoryPath"))
    shaped_primary.append(s)
    if s:
        tops.add(s[0])
        if len(s) == 2:
            leaf_top_count.setdefault(s[1], {}).setdefault(s[0], 0)
            leaf_top_count[s[1]][s[0]] += 1

majority = {leaf: max(d, key=d.get) for leaf, d in leaf_top_count.items()}

def resolve(s):
    """Çoğunluk + leaf==üst kurallarını uygula → kesin kanonik path."""
    if not s or len(s) == 1:
        return s
    top, leaf = s
    if leaf in tops:                 # leaf bir ÜST adı → leaf'siz
        return [top]
    if majority.get(leaf) != top:    # azınlık üst → leaf'siz
        return [top]
    return [top, leaf]

def canon_related(rawlist, primary):
    out = []
    seen = set()
    for p in rawlist or []:
        r = resolve(shape(p))
        if not r:
            continue
        key = tuple(r)
        if r == primary or key in seen:
            continue
        seen.add(key)
        out.append(r)
    return out

# 2. geçiş: yaz
stats = {"tek_seviye": 0, "iki_seviye": 0, "related_toplam": 0}
for b, s in zip(books, shaped_primary):
    primary = resolve(s)
    b["categoryPath"] = primary or []
    b["relatedCategoryPaths"] = canon_related(b.get("relatedCategoryPaths"), primary)
    if primary:
        stats["iki_seviye" if len(primary) == 2 else "tek_seviye"] += 1
    stats["related_toplam"] += len(b["relatedCategoryPaths"])

with open(BOOKS, "w", encoding="utf-8") as f:
    json.dump(books, f, ensure_ascii=False, indent=2)

# doğrulama: hiçbir leaf adı >1 üst'te olmamalı
check = {}
for b in books:
    cp = b["categoryPath"]
    if len(cp) == 2:
        check.setdefault(cp[1], set()).add(cp[0])
    for r in b["relatedCategoryPaths"]:
        if len(r) == 2:
            check.setdefault(r[1], set()).add(r[0])
collisions = {leaf: sorted(t) for leaf, t in check.items() if len(t) > 1}

print(f"BOOKS: {BOOKS}")
print(f"üst kategori: {len(tops)} | {sorted(tops)}")
print("STATS:", json.dumps(stats, ensure_ascii=False))
print("KALAN ÇAKIŞMA:", collisions if collisions else "YOK")
