# Tasarım — search_books `author` filtresi (yazarlık-vs-hakkında)

Tarih: 2026-11-24 · Boyut: küçük değişiklik · Branch: feat/021-content-discovery-v11

## Niyet / neden
"X'in kitapları" denince yalnız X'in **yazdığı** kitaplar dönsün. Bugün yazar adı
serbest `query`'ye gidiyor → BM25 token kirliliği ("Mo Yan" → "Yan Yana"/"Yan
Lianke", 60 sonuç çoğu alakasız). Kök neden: `search_books`'ta author filtresi YOK.

## Hedefler
- Yazar adı → yalnız o yazar(lar)ın eserleri (katı filtre, token kirliliği biter).
- **Yazarlık-vs-hakkında ayrımı:** "X'in kitapları/eserleri/yazarı X" → author filtresi;
  "X hakkında/üzerine" → serbest metin kalır (X'le ilgili her şey, biyografi/inceleme dahil).
- publisher filtresiyle birlikte çalışır; yazar + tür/fiyat/sort birleşebilir.

## Hedef-değil / anti-hedef
- Yazar aggregate'ine yeni alan/tablo/event EKLEMEK (mevcut Author + Product.AuthorIds yeter).
- NLP/kod ile niyet ayrımı — ayrımı **sub-agent LLM prompt'u** yapar (by vs about), kod değil.
- "about" sorgularını bozmak (Dostoyevski hakkındaki kitaplar gelmeye devam eder).

## Kısıt
- Marten/Newtonsoft `AuthorIds` listesini `{$type,$values:[...]}` sarar → jsonb yolu
  `data->'AuthorIds'->'$values'` (düz `data->'AuthorIds'` obje, eşleşmez).
- InvariantGlobalization açık; `NameNormalization` sözleşmesi Author.NormalizedName ile aynı.

## Yaklaşım (tek mantıklı — publisher desenini aynala)
publisher zaten: ad → Marten id SET (`ResolvePublisherIdsAsync`, token ILIKE
NormalizedName) → filtre. Author birebir aynı yol, tek fark eşleşme biçimi:
- **publisher** = tekil `PublisherId` → BM25 quoted-phrase clause.
- **author** = `AuthorIds` LİSTE → jsonb `?|` SQL filtre (moods/themes deseni;
  `?|` OR hazır, token çarpışması yok). Benim kararım; BM25 liste-alan phrase kırılgan.

Reddedilen: (a) yazar adını query'de bırakıp BM25 relevance'a güvenmek — kök sorun bu,
token kirliliği sürer. (b) Product'a yazar-adı düz alanı eklemek — gereksiz denormalize,
AuthorIds + Author zaten var.

## Nasıl (karar verilmiş parçalar)
1. `SearchRequest`: `Author` (string?) alanı ekle.
2. `ProductSearchQueries.SearchAsync`: `ResolveAuthorIdsAsync` (Author aggregate'inde token
   ILIKE NormalizedName; publisher'ın ikizi). Ad verilip 0 eşleşme → `SearchPage.Empty`.
3. `SearchPredicateBuilder.Build`: `authorIds` parametresi → `AddExistsFilter` ile
   `data->'AuthorIds'->'$values' ?| array[id-string...]` SQL filtresi.
4. MCP tool (Catalog `Agents/Queries/SearchBooks.cs`): `author` param ekle → SearchRequest'e geç.
5. ScoutAgent `CatalogSubAgent`: `filters` JSON'una `author` alanı (SearchFilters→MCP args);
   `Instructions` + tool `description`'a kural: **yazarlık ifadesi** ("X'in kitapları/eserleri",
   "yazar X") → `author`'a yaz, query'ye KOYMA; **"X hakkında/üzerine"** → `query`'de bırak.

## Builder'a kalan
- `?|` SQL filtresinde id'lerin string[] olarak geçmesi (mevcut `AddExistsFilter` imzası text[]).
- Test-first (İlke VI): `SearchPredicateBuilder` author filtresi saf unit (by vs about kod değil,
  prompt — test kapsamı dışı); `ResolveAuthorIdsAsync` Marten yolu (canlı doğrulama).
- Canlı doğrulama: "Mo Yan'ın kitapları" → yalnız Mo Yan; "Dostoyevski hakkında" → karışık (by+about).
