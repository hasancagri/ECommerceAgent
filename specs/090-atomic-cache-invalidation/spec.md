# Feature Specification: Atomik Cache Boşaltma (handler-içi publish)

**Feature Branch**: `090-atomic-cache-invalidation`

**Created**: 2026-10-15

**Status**: Draft

**Input**: User description: "Cache boşaltmayı decorator'dan handler-içi atomik publish'e taşı: CachingMessageBus yalnız [Cached] okuma yolunu tutar; [InvalidatesCache] attribute + decorator'daki commit-sonrası publish dalı + IsSuccess koklaması SÖKÜLÜR. Her yazma handler'ı başarılı yazmada CacheInvalidationRequested(tag)'i yazmasıyla AYNI transaction'da yayınlar (atomik). Tek idiom kalır; epoch-key/durable queue/retry/backplane değişmez."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Boşaltma isteği yazmayla atomik (Priority: P1)

Admin bir yazma yapar (ör. kategori güncelle). Boşaltma isteği, yazmanın commit'iyle AYNI
transaction'da kalıcılaşır: commit olduysa istek de kesin kayıtlı; rollback olduysa istek de yok.
Bugünkü "commit oldu ama publish'e gelmeden proses öldü → TTL boyunca bayat" penceresi kapanır.

**Why this priority**: Değişikliğin varlık sebebi — garanti boşluğunu yapısal olarak kapatmak.

**Independent Test**: Boşaltma yayınlayan bir command'in commit'i ile boşaltma zarfının
kalıcılaşmasının tek transaction'da olduğu doğrulanır; başarısız yazmada zarf oluşmaz.

**Acceptance Scenarios**:

1. **Given** cache'te dolu girdi, **When** ilgili yazma command'i başarılı biter, **Then** boşaltma
   isteği yazmayla aynı anda kalıcılaşmıştır ve sonraki okuma taze veri döner.
2. **Given** yazma command'i başarısız sonuç döner (domain hatası), **When** işlem biter,
   **Then** boşaltma isteği yayınlanmaz, cache dokunulmadan kalır.

---

### User Story 2 - Tek boşaltma idiomı (Priority: P2)

Geliştirici yeni yazma yolu açarken (command handler, event consumer, süreç) boşaltmayı HEP aynı
şekilde yapar: handler içinden boşaltma isteği yayınla. Attribute'lu ikinci yol ortadan kalkar;
"boşaltma nasıl yapılır" sorusunun tek cevabı olur.

**Why this priority**: Konvansiyon sadeliği — iki paralel mekanizmanın bakım/öğrenme maliyeti düşer.

**Independent Test**: Repo'da attribute temelli boşaltma kalıntısı kalmadığı; tüm boşaltma
noktalarının aynı deseni kullandığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** söküm tamam, **When** repo taranır, **Then** `[InvalidatesCache]` kullanımı sıfırdır ve
   decorator'da boşaltma dalı yoktur.
2. **Given** mevcut 7 boşaltmalı command (Catalog 3 + Customer 4), **When** her biri başarıyla
   koşar, **Then** eskisiyle AYNI tag'ler boşalır (davranış birebir korunur).

---

### Edge Cases

- Handler başarısız sonuç dönerse boşaltma yayınlanmaz (SC yazısı 1/2). Yayın koşulu handler'ın
  kendi bilgisidir — tip-koklama (sonuç modelinden tahmin) kalmaz.
- Boşaltma işleyicisi (epoch artırma) arızalanırsa mevcut davranış korunur: durable yeniden deneme
  merdiveni + sonunda hata kuyruğu. Bu feature o zinciri DEĞİŞTİRMEZ.
- Okuma yolu (`[Cached]`) hiç etkilenmez; cache anahtarlama (epoch) aynı kalır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Boşaltma isteği, yazma handler'ının İÇİNDEN yayınlanır ve command'in yazmasıyla AYNI
  transaction'da kalıcılaşır (outbox'a atomik katılım).
- **FR-002**: Başarısız yazmada (domain hatası / rollback) boşaltma isteği yayınlanmaz.
- **FR-003**: `[InvalidatesCache]` attribute'u ve decorator'daki commit-sonrası publish dalı +
  sonuç-tipi koklaması tümüyle sökülür; decorator yalnız `[Cached]` okuma yolunu işler.
- **FR-004**: Mevcut 7 boşaltmalı command (Catalog: yazar-oluştur, kategori-oluştur/güncelle;
  Customer: adres ekle/güncelle/sil/varsayılan-yap) aynı tag'lerle boşaltmaya devam eder.
- **FR-005**: Boşaltma alt-zinciri (durable local queue taşıma, epoch artırma, backplane yayılımı,
  retry merdiveni, hata kuyruğu) DEĞİŞMEDEN kalır.
- **FR-006**: Konvansiyon belgeleri yeni tek idiomu anlatacak şekilde güncellenir (decorator-cache
  maddesi: boşaltma artık handler-içi; "decorator-dışı elle publish" istisna maddesi genel kural olur).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Boşaltmalı 7 command'in tamamında, başarılı yazma sonrası ilgili cache'in taze veri
  döndüğü mevcut test/senaryolarla doğrulanır — davranış regresyonu sıfır.
- **SC-002**: Yazma commit'i ile boşaltma isteğinin kalıcılaşması arasında zaman penceresi kalmaz
  (tek transaction) — "commit var, boşaltma yok" durumu yapısal olarak imkânsız.
- **SC-003**: Repo'da attribute temelli boşaltma kalıntısı sıfır (tarama boş döner).
- **SC-004**: Tüm çözüm derlenir, tüm testler yeşil.

## Assumptions

- Mesaj altyapısı, handler-içinden yayınlanan mesajı command'in transaction'ına (outbox) atomik
  dahil edebiliyor — bu yeteneğin varlığı tasarım konuşmasında doğrulandı; biçimi (cascading dönüş
  vs handler-içi publish) implementasyon kararıdır.
- Kapsam yalnız command temelli boşaltma taşıması; zaten elle publish yapan yollar (ör. import
  süreci) davranışça değişmez, yalnız "istisna" statüsünden "genel kural" statüsüne geçer.
- Artefakt ölçeği "küçük" (yeni aggregate/tablo/event/kontrat yok): spec + tasks yeterli, plan
  atlanabilir.
