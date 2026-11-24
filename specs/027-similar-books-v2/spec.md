# Feature Specification: similar_books V2 (vektör) — Dilim-3

**Feature Branch**: `feat/027-similar-books-v2` · **Created**: 2026-11-24 · **Status**: Draft · **Ölçek**: küçük (spec+tasks)

**Input**: 027 Dilim-3 — `similar_books`'u etiket-BM25 örtüşmesinden ÇAPA VEKTÖRÜNE kNN'e evir. Embedding
omurgası (025) hazır; benzerlik artık anlam-temelli (ortak etiket şart değil). İmza (productId, count) + hard
filtre + WorkKey tekilleştirme + stok-önce KORUNUR. Kilitli: saf vektör (boost yok), eşik yok, V1 BM25 sök.

## User Scenarios & Testing

### User Story 1 — Anlam-temelli benzerlik (Priority: P1)

Kullanıcı Kâşif'e "şuna benzer kitap" der. V2 çapa kitabın VEKTÖRÜNE en yakın kitapları döndürür — ortak
etiket (Themes/Moods) paylaşmasalar bile anlamca yakın olanlar. (Tolstoy↔Dostoyevski etiketleri ayrıksa bile
aynı bölgede → yakalanır.)

**Independent Test**: Bilinen bir kitabın `similar_books`'u → komşular anlamca ilgili (V1'in etiket-körlüğünün
kaçırdıkları dahil); kendi baskıları gelmez, hepsi uyumlu seviye.

**Acceptance Scenarios**:
1. **Given** çapa embed'li, **When** similar_books(productId), **Then** vektör-yakın kitaplar (kendi WorkKey
   baskıları HARİÇ), eser başına 1, stoktakiler önce.
2. **Given** çapanın embedding'i yok (nadir), **When** similar_books, **Then** boş sonuç (hata değil).

### Edge Cases
- Çapa bulunamaz → NotFound (V1 davranışı korunur).
- Hiç uyumlu komşu yok (hard filtre hepsini eler) → boş.

## Requirements

- **FR-001**: Benzerlik çapa kitabın `product_embedding` vektörüne kNN (kosinüs) ile belirlenMELİ; etiket-BM25
  örtüşmesi KALDIRILMALI.
- **FR-002**: Hard filtre KORUNMALI: çapanın kendi WorkKey baskıları hariç + seviye uyum grubu + (varsa) aynı dil.
- **FR-003**: Eser başına tek sonuç (`DISTINCT ON WorkKey`, çapaya en yakın baskı) + sıralama stok-önce → yakınlık.
- **FR-004**: MCP tool imzası (`productId`, `count`) + dönüş şekli DEĞİŞMEMELİ (ScoutAgent/024 akışı bozulmaz).
- **FR-005**: Çapanın embedding'i yoksa boş dönMELİ (hata değil); eşik/cutoff UYGULANMAZ (top-K yeter).
- **FR-006**: Objektif boost (yazar/çevirmen/tür) KALDIRILMALI (vektör zaten kodluyor); `SimilarBooksOptions`
  yalnız sayı sınırı (`DefaultCount`/`MaxCount`) tutMALI.

## Success Criteria

- **SC-001**: V1'in ortak-etiket-yoksa kaçırdığı anlamca-yakın kitaplar V2'de komşu olarak çıkar (canlı örnek).
- **SC-002**: Dönen kitaplar çapanın kendi baskılarını İÇERMEZ + hepsi uyumlu seviye (hard filtre).
- **SC-003**: 024 ScoutAgent "benzer kitap" akışı (React) bozulmadan çalışır (imza sabit).

## Assumptions

- Embedding omurgası (025) canlı, katalog embed'li (eksikler worker'la kapanır).
- V2 saf vektör; same-author hafif boost gerekirse sonra (ölçümle), şimdi yok.
- Küçük ölçek: yeni tablo/event/kontrat yok (`product_embedding` reuse, imza sabit) → spec+tasks, plan yok.
