#!/usr/bin/env python3
"""Open Library'den GERÇEK-STANDART kitap verisi çeker → Catalog seeder'ın books.json'u (v2 şema).

Neden: kitapyurdu'yu scrape etmek yerine (ToS/telif), açık + legal Open Library Search API'sinden
gerçek ISBN + yazar + YAYINEVİ + kapak + kategori çeker. 052'nin uydurmaları (publisher md5%4,
rol-etiketi parse) GEREKMEZ — veri gerçek. Yalnız FİYAT üretilir (OL'de fiyat yok).
Kullanım: yalnız eğitim/portföy (ticari değil); kapaklar OL link'i (üçüncü-taraf telifi).

Kaynak: search.json — tek yanıtta isbn[]+publisher[]+cover_i+subject[]+author_name[] gelir; bulk
dump'a (11GB+) gerek yok, sadece nihai books.json saklanır (MB'ler).

Çıktı şeması (ImportBook birebir tüketir): isbn, title, authors[], publisher, priceTry, imageUrl,
categoryMid, categoryLeaf.

Kullanım:
  python3 scripts/book-import/fetch_openlibrary.py \
    --out src/services/catalog/Catalog.Api/Seeding/Data/books.json --target 10000
"""

import argparse
import hashlib
import json
import sys
import time
import urllib.parse
import urllib.request

# categoryMid havuzu = çekilecek konular (Open Library subject slug'ları). Her konu bir üst-kategori olur.
SUBJECTS = [
    "fiction", "science", "history", "biography", "children",
    "philosophy", "poetry", "business", "technology", "art",
    "psychology", "fantasy", "mystery", "romance", "cooking",
]

# OL etiketi: açıklayıcı User-Agent ister (iletişim için). Rate-limit'e kibar davran.
USER_AGENT = "ECommerceAgent-BookSeed/1.0 (education; contact: hasancagridemiriz@gmail.com)"
FIELDS = "key,title,author_name,isbn,publisher,cover_i,subject,first_publish_year,language"
PAGE_SIZE = 100
THROTTLE_SEC = 1.0          # sayfalar arası bekleme (rate-limit)
MAX_PAGES_PER_SUBJECT = 150  # emniyet üst sınırı (konu başına; 20k hedefe ulaşabilsin)


def _pick_isbn(isbns):
    """ISBN-13 tercih (13 hane), yoksa ilk geçerli. ProductId bundan türer."""
    if not isbns:
        return None
    thirteen = [i for i in isbns if len(i) == 13 and i.isdigit()]
    if thirteen:
        return thirteen[0]
    ten = [i for i in isbns if len(i) == 10]
    return ten[0] if ten else isbns[0]


def _price_try(isbn):
    """Fiyat OL'de yok → ISBN-kararlı gerçekçi bant (40–400₺, .90 kuruş). Deterministik = tekrar-üretilebilir."""
    h = int(hashlib.md5(isbn.encode("utf-8")).hexdigest(), 16)
    base = 40 + (h % 361)          # 40..400
    return round(base - 0.10, 2)   # ...,90 hissi


def _leaf(subject_mid, doc_subjects):
    """categoryLeaf = doc'un ilk özgül, temiz subject'i (mid'den farklı). Yoksa mid."""
    for s in (doc_subjects or []):
        if not isinstance(s, str):
            continue
        s = s.strip()
        # gürültü ele: çok uzun, boş
        if not s or len(s) > 40:
            continue
        if s.lower() == subject_mid.lower():
            continue
        return s
    return subject_mid.capitalize()


def _fetch_page(subject, page):
    q = urllib.parse.urlencode({
        "q": f"subject:{subject}",
        "fields": FIELDS,
        "limit": PAGE_SIZE,
        "page": page,
        "lang": "en",
    })
    url = f"https://openlibrary.org/search.json?{q}"
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _valid(doc):
    """Dolu kayıt: isbn + publisher + cover + title + İngilizce."""
    isbn = _pick_isbn(doc.get("isbn"))
    langs = doc.get("language") or []
    return (
        isbn is not None
        and doc.get("publisher")
        and doc.get("cover_i")
        and doc.get("title")
        and (not langs or "eng" in langs)
    )


def _to_record(doc, subject_mid):
    isbn = _pick_isbn(doc["isbn"])
    return {
        "isbn": isbn,
        "title": (doc.get("title") or "").strip(),
        "authors": [a.strip() for a in (doc.get("author_name") or []) if a and a.strip()] or ["Unknown"],
        "publisher": (doc["publisher"][0] or "").strip() or "Unknown",
        "priceTry": _price_try(isbn),
        "imageUrl": f"https://covers.openlibrary.org/b/id/{doc['cover_i']}-L.jpg",
        "categoryMid": subject_mid.capitalize(),
        "categoryLeaf": _leaf(subject_mid, doc.get("subject")),
    }


def fetch(target, per_subject):
    out = []
    seen = set()
    for subject in SUBJECTS:
        kept = 0
        page = 1
        while kept < per_subject and page <= MAX_PAGES_PER_SUBJECT and len(out) < target:
            try:
                data = _fetch_page(subject, page)
            except Exception as exc:  # ağ/timeout → bu konuyu bırak, devam
                print(f"  ! {subject} p{page} hata: {exc}", file=sys.stderr)
                break
            docs = data.get("docs") or []
            if not docs:
                break
            for doc in docs:
                if not _valid(doc):
                    continue
                rec = _to_record(doc, subject)
                if rec["isbn"] in seen:
                    continue
                seen.add(rec["isbn"])
                out.append(rec)
                kept += 1
                if kept >= per_subject or len(out) >= target:
                    break
            print(f"  {subject}: sayfa {page} → toplam {len(out)} (bu konu {kept})", file=sys.stderr)
            page += 1
            time.sleep(THROTTLE_SEC)
        if len(out) >= target:
            break
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, help="books.json çıktı yolu")
    ap.add_argument("--target", type=int, default=10000, help="hedef toplam kitap")
    args = ap.parse_args()

    per_subject = max(1, args.target // len(SUBJECTS))
    print(f"Hedef {args.target}, konu başına ~{per_subject}, {len(SUBJECTS)} konu...", file=sys.stderr)

    books = fetch(args.target, per_subject)

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(books, f, ensure_ascii=False, indent=2)
    print(f"\n# {len(books)} kitap yazıldı → {args.out}", file=sys.stderr)


if __name__ == "__main__":
    main()