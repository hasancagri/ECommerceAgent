# Quickstart — Kâşif Tipli Niyet-Çıkarma doğrulama

Önkoşul: Aspire AppHost canlı (`dotnet run --project src/aspire/AppHost/AppHost.csproj`); gateway `:5060`.

## 1. Birim testler (test-first, İlke VI)
```bash
dotnet test tests/ScoutAgent.Api.Tests/ScoutAgent.Api.Tests.csproj --filter "FilterInterpreter"
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj --filter "SearchPredicateBuilder"
```
Beklenen: FilterInterpreter (hint parse + grup-null düzleme) yeşil; Catalog 26 yeşil.

## 2. Tur-1 extract (tipli çıktı)
```bash
curl -sk http://localhost:5060/api/chat/v1/extract -X POST -H "Content-Type: application/json" \
  -d '{"query":"Mo Yanın kitapları"}'
```
Beklenen: `text.author="Mo Yan"`, diğer gruplar null (uydurma yok).

## 3. Tur-2 search (yazarlık — SC-001)
extract çıktısını `/search`'e ver:
```bash
curl -sk http://localhost:5060/api/chat/v1/search -X POST -H "Content-Type: application/json" \
  -d '{"filters":{"text":{"author":"Mo Yan"},"page":1,"pageSize":10}}'
```
Beklenen: dönen kartların **%100'ü Mo Yan** yazımı ("Yan Yana"/"Yan Lianke" YOK). 10 farklı yazarla tekrarla.

## 4. by/about ayrımı (SC-002)
```bash
# hakkında → karışık (by+about), author filtresi UYGULANMAZ
curl ... -d '{"query":"Dostoyevski hakkında kitaplar"}'   # → text.query="Dostoyevski", author=null
# yazarlık → yalnız Dostoyevski
curl ... -d '{"query":"Dostoyevskinin kitapları"}'         # → text.author="Dostoyevski"
```
Her birini 3 kez; tutarlı doğru (spike metodolojisi).

## 5. Çok-kriter + uydurma yok (SC-003)
```bash
curl ... -d '{"query":"İş Bankası hariç 300 TL altı ciltli kitaplar"}'
```
Beklenen: `publisher.exclude="İş Bankası"`, `priceHint="300…altı"`, `bindingHint="ciltli"`;
`sortHint=null`, `flags` null/false (sorguda SORT/STOK/İNDİRİM YOK → uydurma yok).

## 6. React iki-tur (canlı, ekrandan)
`:5173` → sohbete karmaşık sorgu → React `/extract` → typed → `/search` → kartlar + "anladım" filtre gösterimi.
Müşteri refleksiyle: yazar/by-about/çok-kriter sorguları; her sonuç doğru + özür/kirlilik yok.

## Not
- Detay sözleşme: [contracts/extract-search.md](./contracts/extract-search.md); eşleme: [data-model.md](./data-model.md).
- Spike (`SpikeExtract.cs`) implementation'da SİLİNİR.
