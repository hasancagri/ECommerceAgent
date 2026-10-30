#!/usr/bin/env python3
"""Kitapyurdu katalog xlsx → Catalog seed books.json (Kitapyurdu şeması).

Kaynak xlsx: her sheet bir yayınevi (sheet adı = yayınevi). Kolonlar:
  A Kitap Adı · B Yazar · C ISBN · D Çevirmen · E Kategori · F İlgili Kategoriler ·
  G Sayfa Sayısı · H Ort.Okuma · I Dil · J Yayın Tarihi · K Cilt Tipi ·
  L Liste Fiyatı · M Site Fiyatı · N İndirim% · O Puan · P Açıklama · Q Yazar Açıklaması

Kararlar: priceTry = Liste Fiyatı (gerçek satış). Site/İndirim%/Puan/Ort.Okuma atılır.
Kategori kökü "Kitap" atılır. Stok Excel'de yok → ISBN'den deterministik 5-200 (re-run stabil).
Eleme: ISBN yok / Liste Fiyatı yok veya <=0 / mükerrer ISBN.

Kullanım:
  python3 scripts/book-import/kitapyurdu_to_books_json.py [SRC.xlsx] [OUT.json]
Varsayılan SRC = Desktop'taki Kitapyurdu_Katalog.xlsx, OUT = Catalog seed books.json.
openpyxl GEREKMEZ — ham XML parse (stdlib)."""
import json, zipfile, re, sys, os
import xml.etree.ElementTree as ET

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser(
    "~/Desktop/ECommerce Notlaır/Kitapyurdu_Katalog.xlsx")
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    REPO, "src/services/catalog/Catalog.Api/Seeding/Data/books.json")

NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
RNS = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
COL = dict(title="A", author="B", isbn="C", translator="D", category="E", related="F",
           pages="G", lang="I", pubdate="J", binding="K", listprice="L", desc="P", bio="Q")

z = zipfile.ZipFile(SRC)

ss = []
if "xl/sharedStrings.xml" in z.namelist():
    r = ET.fromstring(z.read("xl/sharedStrings.xml"))
    for si in r.findall(NS + "si"):
        ss.append("".join(t.text or "" for t in si.iter(NS + "t")))

def cell_text(c):
    t = c.get("t")
    if t == "inlineStr":
        isn = c.find(NS + "is")
        return "".join(x.text or "" for x in isn.iter(NS + "t")) if isn is not None else ""
    v = c.find(NS + "v")
    if v is None:
        return ""
    if t == "s":
        return ss[int(v.text)]
    return v.text or ""

def row_cells(row):
    out = {}
    for c in row:
        ref = c.get("r")
        if ref:
            out[re.match(r"[A-Z]+", ref).group(0)] = cell_text(c)
    return out

wb = ET.fromstring(z.read("xl/workbook.xml"))
rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
rid_to_target = {rel.get("Id"): rel.get("Target") for rel in rels}
sheets = []
for s in wb.iter(NS + "sheet"):
    target = rid_to_target[s.get(RNS + "id")].lstrip("/")
    if not target.startswith("xl/"):
        target = "xl/" + target
    sheets.append((s.get("name"), target))

def strip_quotes(s):
    s = (s or "").strip()
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        s = s[1:-1].strip()
    return s

def parse_price(s):
    s = (s or "").strip()
    if not s:
        return None
    s = s.replace("TL", "").replace("₺", "").replace(" ", "").strip()
    s = s.replace(".", "").replace(",", ".")  # 1.250,00 -> 1250.00
    try:
        v = float(s)
        return round(v, 2) if v > 0 else None
    except ValueError:
        return None

def parse_pages(s):
    s = (s or "").strip()
    m = re.search(r"\d+", s)
    return int(m.group(0)) if m else None

def parse_date(s):  # dd.MM.yyyy -> yyyy-MM-dd
    m = re.match(r"(\d{1,2})\.(\d{1,2})\.(\d{4})", (s or "").strip())
    if not m:
        return None
    d, mo, y = m.groups()
    return f"{y}-{int(mo):02d}-{int(d):02d}"

def parse_path(s):  # "Kitap > Edebiyat > Roman (Yerli)" -> ["Edebiyat","Roman (Yerli)"]
    segs = [p.strip() for p in (s or "").split(">") if p.strip()]
    if segs and segs[0].lower() == "kitap":
        segs = segs[1:]
    return segs

def tr_lower(s):
    return s.replace("I", "ı").replace("İ", "i").lower()

def tr_word(w):
    w = tr_lower(w.strip())
    return ({"i": "İ", "ı": "I"}.get(w[0], w[0].upper()) + w[1:]) if w else w

def tr_titlecase(s):  # çok-dilli hücre: virgülle ayır, her parçayı normalize et
    s = (s or "").strip()
    if not s:
        return None
    parts = []
    for part in s.split(","):
        words = [tr_word(w) for w in part.split() if w.strip()]
        if words:
            parts.append(" ".join(words))
    return ", ".join(parts) or None

def strip_binding_suffix(title, binding):
    t = (title or "").strip()
    if binding:
        suffix = f"({binding.strip()})"
        if t.lower().endswith(suffix.lower()):
            t = t[: -len(suffix)].strip()
    return t

def split_translators(s):
    return [p.strip() for p in re.split(r"[;,/]", (s or "").strip()) if p.strip()]

books = []
stats = dict(rows=0, no_isbn=0, no_price=0, dup_isbn=0, ok=0, by_pub={})
seen_isbn = set()

for name, target in sheets:
    root = ET.fromstring(z.read(target))
    sd = root.find(NS + "sheetData")
    if sd is None:
        continue
    rows = sd.findall(NS + "row")
    for row in rows[1:]:  # başlığı atla
        rc = row_cells(row)
        stats["rows"] += 1
        isbn = (rc.get(COL["isbn"], "") or "").strip()
        if not isbn:
            stats["no_isbn"] += 1
            continue
        price = parse_price(rc.get(COL["listprice"], ""))
        if price is None:
            stats["no_price"] += 1
            continue
        if isbn in seen_isbn:
            stats["dup_isbn"] += 1
            continue
        seen_isbn.add(isbn)

        author = strip_quotes(rc.get(COL["author"], "")).strip()
        authors = [author] if author else ["Bilinmiyor"]
        bio = strip_quotes(rc.get(COL["bio"], "")).strip()
        author_bios = {authors[0]: bio} if bio and author else {}

        primary = parse_path(rc.get(COL["category"], ""))
        related = []
        for chunk in (rc.get(COL["related"], "") or "").split("|"):
            p = parse_path(chunk)
            if p and p != primary and p not in related:
                related.append(p)

        # binding yalnız title suffix temizliği için okunur ("(Karton Kapak)" ekini atar); stock+binding
        # kolonları şemadan söküldü (Stock BC kaldırıldı; cilt facet'i gereksiz) → çıktıya yazılmaz.
        binding = (rc.get(COL["binding"], "") or "").strip() or None
        books.append({
            "isbn": isbn,
            "title": strip_binding_suffix(strip_quotes(rc.get(COL["title"], "")), binding),
            "authors": authors,
            "authorBios": author_bios,
            "translators": split_translators(rc.get(COL["translator"], "")),
            "publisher": name.strip(),
            "priceTry": price,
            "pageCount": parse_pages(rc.get(COL["pages"], "")),
            "publishedOn": parse_date(rc.get(COL["pubdate"], "")),
            "language": tr_titlecase(rc.get(COL["lang"], "")),
            "categoryPath": primary,
            "relatedCategoryPaths": related,
            "imageUrl": None,
            "description": strip_quotes(rc.get(COL["desc"], "")).strip(),
        })
        stats["ok"] += 1
        stats["by_pub"][name] = stats["by_pub"].get(name, 0) + 1

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(books, f, ensure_ascii=False, indent=2)

print(f"SRC: {SRC}")
print(f"OUT: {OUT}")
print("STATS:", json.dumps(stats, ensure_ascii=False))
print(f"TOPLAM satılabilir: {len(books)} | ilgili-kategorili: "
      f"{sum(1 for b in books if b['relatedCategoryPaths'])} | "
      f"bio: {sum(1 for b in books if b['authorBios'])} | "
      f"çevirmen: {sum(1 for b in books if b['translators'])}")
print("diller:", sorted({b["language"] for b in books if b["language"]}))
