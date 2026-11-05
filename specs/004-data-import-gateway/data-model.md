# Data Model — 004 Temel Veri + Gateway

## YENİ: ImportJob (aggregate, Catalog BC)

Excel import işinin yaşam döngüsü. `AggregateRoot`'tan türer; Marten belgesi (catalogDb).

| Alan | Tip | Not |
|---|---|---|
| Status | enum: Queued / Running / Completed / Failed | geçişler yalnız davranış metotlarından |
| FileName | string | yüklenen dosyanın adı (iz) |
| TempFilePath | string | geçici disk yolu; iş bitince silinir |
| Processed / Added / Skipped / Errored | int sayaçlar | Skipped = ISBN mevcut; aralıklı flush |
| Errors | liste: (Sheet, Row, Reason) | satır hataları; iş durmaz |
| StartedAt / FinishedAt | UTC | — |

**Davranışlar (test-first, İlke VI):** `Create` (Queued doğar) · `Start` (Queued→Running) · `RecordProgress(added, skipped, errors)` · `Complete` · `Fail(reason)`.
**Invariant'lar:** geriye durum geçişi yok; sayaçlar negatif olamaz; `Complete/Fail` sonrası mutasyon reddi. **Tek-aktif-iş** guard'ı handler'da sorguyla (aggregate kendi dışını göremez) + `Create` öncesi kontrol.

## DEĞİŞEN: ProductAdded (kontrat, Shared.IntegrationEvents)

`ProductAdded(Barcode, ProductId)` → **additive**: `string? CoverSourceUrl = null`. Eski tüketici kırılmaz; File, null görürse eski davranış (staging/R2 yoklama), doluysa indirme hattı (R5).

## DEĞİŞEN: Product (Catalog) — davranış mevcut, YENİ ALAN YOK

Import mevcut davranışları çağırır: `Create(name, gtin, price?, description)` · `SetTranslators` · `SetCategory` · `SetRelatedCategories` · `SetAuthors` · `SetPublisher` · `SetBinding` · `SetPopularity` · `SetImage` (CoverIngested tüketicisinden). Sade künye kararı: hiçbir yeni Product alanı açılmaz.

## MEVCUT: Author / Publisher / Category (Catalog)

Get-or-create hedefleri; ad normalizasyonu mevcut `NameNormalization` ile. Marten leaf==mid çift-Store unique tuzağına dikkat (bilinen gotcha — aynı batch'te aynı ada iki Store yok). Author bio: Author'da alan varsa `Yazar Açıklaması` doldurur; yoksa eklenmez.

## MEVCUT: FileAsset (File BC)

Değişmez. Yeni kullanım: indirme hattı (R5) aynı `Create/AddOrReplaceLocation` yoluna girer.

## Gateway — veri modeli YOK

BC değil; DB'siz. Rota tablosu + admin API anahtarı config'te (Options pattern; anahtar secret, İlke VIII).