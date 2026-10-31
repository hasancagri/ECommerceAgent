# Quickstart — 096 doğrulama kılavuzu (095 kapanışı DAHİL)

Ön koşullar: Docker · **Ollama kurulu + `ollama pull bge-m3`** · temiz catalogDb/discountDb-kalıntısız
ortam (dev reset — 095 temiz-DB varsayımı burada mühürlenir).

## 1. Build + testler + guard'lar

```bash
dotnet build && dotnet test
scripts/check-claude-spec-links.sh && scripts/check-flow-links.sh
```

## 2. Ollama KAPALI açılış (US2 / SC-004)

1. Ollama'yı durdur; `dotnet run --project src/aspire/AppHost/AppHost.csproj`.
2. Beklenen: sistem yeşil, seed dolar (095 davranışı), Catalog logunda embed erişim hatası UYARI
   olarak akar, host ÇÖKMEZ; `mt_doc_productembedding` boş.

## 3. Ollama AÇIK dolum (US1 / SC-001, SC-005)

1. `ollama serve` (model çekili) — sistem ÇALIŞIRKEN aç; müdahale etme.
2. Beklenen: sonraki döngüde dolum başlar; log ilerlemesi; bitişte kayıt sayısı = ürün sayısı (11.438).
3. Süre notu al (SC-005: dakikalar mertebesi).

```sql
select count(*) from catalog.mt_doc_productembedding;  -- = 11438
```

## 4. İdempotensi + tazelik (SC-002/003)

1. AppHost'u yeniden başlat → dolum döngüsü "aday yok" demeli (log'da 0 üretim).
2. Bir ürünün Description'ını DB'den değiştir (dev-elle) → sonraki taramada YALNIZ o kaydın
   `UpdatedAtUtc`/`ContentHash`'i değişir.
3. `EmbeddingOptions.Model`'i değiştir (ör. qwen3-embedding) → tüm kayıtlar tazelenir (örneklem kontrol).

## 5. 095 ertelenen canlı doğrulama (SC-006 — specs/095 tasks T014/T020/T026)

Aynı açılışta [specs/095-catalog-book-domain/quickstart.md](../095-catalog-book-domain/quickstart.md)
§2-§6 koşulur (Discount maddeleri DÜŞTÜ — BC söküldü):

- §2 seed sayımları: 11.438 ürün; kategori 31 kök + ~180 alt, çift kayıtsız; tek-leaf/related örnekleri;
  ikinci açılış idempotensisi.
- §3 event alanları: `Categories`/`CategoryIds` = leaf+ata (RabbitMQ UI örneği).
- §5 kapak hattı: `CoverIngested` → ImageUrl dolumu; kuyruk birikmiyor (FileConsumers restorasyonu).
- §4/§6: Library alarm davranışı + FLOW guard'ları.

## 6. Kapanış

- SC tablosu işaretlenir; specs/095 tasks.md T014/T020/T026 `[X]` yapılır.
- ADR (`docs/adr/adr-search-fold-to-postgres.md`) README index'ine eklenmiş olmalı.

## 7. Koşu sonucu (2026-10-31 — temiz-DB Aspire koşusu)

- **SC-001 PASS:** 11.438/11.438 vektör (`mt_doc_productembedding`); ilk tur 9.287 (seed sürerken) + ikinci tur 2.152.
- **SC-005 PASS:** tam dolum ~14 dk (09:59→10:13, ~830 vektör/dk; bge-m3, batch 64, M-serisi).
- **SC-002 PASS:** dolum sonrası 10+ boş tur 0 yazım; AppHost restart sonrası da 0 yeniden üretim (seed: "0 eklendi, 11438 zaten vardı").
- **SC-003 PASS (metin ekseni):** 1 ürünün Description'ı SQL'le değişti → tek o kayıt tazelendi (hash `783C…`→`7899…`, 07:13 sonrası yazım sayısı=1). Model ekseni unit-test'le kapalı (`NeedsRefresh`), tam re-embed koşusu bilinçli atlandı (~14 dk).
- **SC-004 PASS:** Ollama kapalıyken "Connection refused" UYARI olarak aktı, host çökmedi, seed doldu; Ollama dönünce müdahalesiz tazeleme.
- **SC-006 PASS (095 kapanışı):** seed sayımları 11.438 ürün, kategori 32 kök (31+Genel) + 182 alt, 0 çift; "Popüler Kültür→Kültür" leaf+ata örneği; ikinci açılış idempotent. Kapak HATTI uçtan uca sağlam (R2-kesişen 7 ISBN: `CoverIngested`→`ImageUrl` doldu; kuyruk/DLQ temiz) — kalan ürünlerde kapak YOK ve bu BİLİNÇLİ: `books.json` `imageUrl` alanı kullanıcı kararıyla boş (veri yok, kod boşluğu değil).
- **Koşuda yakalanan tuzak (düzeltildi):** temiz-sayfa söküm Catalog `Program.cs`'te `UseAuthentication/UseAuthorization`'ı kayıtsız bırakmıştı — Catalog açılışta çöküyordu; ApiKey-only desene çekildi (basket/order/payment emsali).
