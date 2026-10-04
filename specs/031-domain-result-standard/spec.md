# Feature Specification: Domain Sonuç Sarmalama Standardı (ResultDomain) + Aggregate Klasör Kuralları

**Feature Branch**: `031-domain-result-standard`

**Created**: 2026-08-08

**Status**: Draft

**Input**: User description: "Domain içinde dönen her sonucu ResultDomain ile sar; aggregate-klasör ve ValueObjects yerleşim kurallarını CLAUDE.md'ye kural olarak yaz. Hem PaymentGateway hem ECommerceAgent."

## Bağlam

İki repo (**PaymentGateway** + **ECommerceAgent**) mimariyi paylaşır: Vertical Slice + CQRS,
zengin aggregate'ler, Result pattern. Şu an domain katmanında tutarsızlık var: bazı aggregate davranış
metotları sonucu `ResultDomain` ile sararken (ör. `SettlementAccount.UpdateDetails`), bazıları ham
enum/bool/değer döner (ör. `DomainControlChallenge.Verify` → `ChallengeOutcome`, `Merchant.TryActivate`
→ `bool`). Bu, çağıran (handler) tarafında karışık sonuç işleme ve öngörülemez hata mesajı akışı üretir.

Bu özellik iki şeyi standartlaştırır ve yazılı kural haline getirir:
1. **Sonuç sözleşmesi**: aggregate davranış/fabrika metotları tek tip `ResultDomain`/`ResultDomain<T>` döner.
2. **Klasör düzeni**: `Domains/` hemen altındaki her klasör tek bir AggregateRoot'a aittir; value
   object'ler ilgili aggregate'in `ValueObjects/` alt klasörüne konur.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tek tip domain sonuç sözleşmesi (Priority: P1)

Bir geliştirici (bakımcı) olarak, bir aggregate davranış metodunu çağırdığımda dönen tipin her zaman
`ResultDomain` veya `ResultDomain<T>` olmasını isterim; böylece başarı/başarısızlık ve mesajları tek
desenle işlerim, ham enum/bool'u yorumlamak zorunda kalmam.

**Why this priority**: Tutarsız sonuç tipleri handler'larda dağınık kontrol akışı ve sessiz hata yolları
üretir. Standart, en yüksek değeri sağlayan çekirdek sözleşmedir; diğer her şey buna dayanır.

**Independent Test**: Bir aggregate'in ham dönen bir davranış metodu (ör. `DomainControlChallenge.Verify`)
`ResultDomain<ChallengeOutcome>` dönecek şekilde refactor edilir; çağıran handler `IsSuccess`/`Data`/
`Messages` deseniyle güncellenir; birim testleri yeşil kalır. Bu tek slice bile sözleşmeyi kanıtlar.

**Acceptance Scenarios**:

1. **Given** ham `ChallengeOutcome` dönen `Verify`, **When** metot `ResultDomain<ChallengeOutcome>`
   dönecek şekilde sarılır, **Then** başarı `Ok(outcome)`, çağıran `result.Data` ile outcome'a erişir.
2. **Given** `bool` dönen `Merchant.TryActivate`, **When** `ResultDomain` dönecek şekilde sarılır,
   **Then** aktivasyon olmadıysa `Error(messages)`, olduysa `Ok()` döner ve handler mesajları taşır.
3. **Given** bir fabrika metodu (`Issue`/`Create`), **When** standarda uyarlanır, **Then**
   `ResultDomain<T>` döner (başarıda `Ok(data)`).

### User Story 2 - Yazılı kod standardı (Priority: P1)

Bir geliştirici olarak, bu üç kuralın (sonuç-sarmalama, aggregate-klasör, ValueObjects yerleşimi)
her iki repo'nun `CLAUDE.md`'sinde açıkça yazılı olmasını isterim; böylece gelecekteki katkılar ve
AI ajanları kuralı bilir ve bozmaz.

**Why this priority**: Kural yazılı değilse standart tekrar erozyona uğrar. Dokümantasyon, refactor'un
kalıcılığını sağlar.

**Independent Test**: Her iki `CLAUDE.md` ilgili kural maddelerini içerir; yeni bir domain metodu
eklendiğinde kuralın uygulanıp uygulanmadığı doküman referansıyla denetlenebilir.

**Acceptance Scenarios**:

1. **Given** güncellenmiş `CLAUDE.md`, **When** okunur, **Then** üç kural da net, örnekli ve
   muafiyetleriyle (saf getter/sorgu muaf) belirtilmiştir.

### User Story 3 - Klasör düzeni uyumu (Priority: P2)

Bir geliştirici olarak, `Domains/` altındaki her klasörün tek bir aggregate'e ait olmasını ve value
object'lerin `ValueObjects/` altında durmasını isterim; böylece iş süreçlerini klasör ağacından
okuyabilirim.

**Why this priority**: İç içe aggregate ve dağınık VO okunabilirliği düşürür. PaymentGateway'de
(Merchant BC) bu zaten düzeltildi; kural repo genelinde doğrulanır ve ECommerce'de uygulanır.

**Independent Test**: Her `Domains/<X>/` klasöründe en fazla bir `: AggregateRoot` sınıfı bulunur;
loose VO dosyaları `ValueObjects/` altına taşınır; build yeşil kalır.

**Acceptance Scenarios**:

1. **Given** bir aggregate klasörü, **When** taranır, **Then** tam olarak bir AggregateRoot içerir;
   ikinci bir aggregate iç içe değildir.
2. **Given** bir standalone value object dosyası, **When** yerleşim denetlenir, **Then** ilgili
   aggregate'in `ValueObjects/` alt klasöründedir.

### Edge Cases

- **Saf sorgu/getter**: `GetCommissionRate` gibi hesap/lookup metotları muaftır — sarılmaz (aksi
  gürültü üretir). Kural yalnız durum değiştiren davranış + fabrika metotlarını kapsar.
- **Asla başarısız olmayan fabrika**: `ActivationTicket.Issue` gibi doğrulama yapmayan saf inşa
  metodu için `ResultDomain<T>.Ok(...)` sarımı yine uygulanır (tek tiplik için); mesaj listesi boş.
- **Outcome-enum**: `ChallengeOutcome` gibi çok-durumlu domain sonucu hata değildir; `Ok(outcome)`
  ile sarılır, hata eşlemesi yapılmaz — yorumu çağıran verir.
- **SharedKernel**: Birden çok aggregate'in paylaştığı VO/enum (ör. Commission `Criteria`,
  `TransactionRegion`) tek aggregate'e ait olmadığından `SharedKernel/` altında kalır — bu klasör
  aggregate-klasör kuralının bilinçli istisnasıdır.
- **Domain service / infra**: `BankRouter` (saf hesap domain service), seeder, MCP tool sınıfları
  aggregate değildir; aggregate-klasör kuralı kapsamı dışıdır (yerinde kalır).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-000 (kapsam)**: Standart yalnız **handler'dan (Command/Query slice handler) çağrılan** aggregate
  davranış/fabrika metotlarını kapsar. Call-site kriteri belirleyicidir: bir aggregate metodu bir
  handler tarafından çağrılıyorsa sarma zorunludur. Hiçbir handler'dan erişilmeyen metotlar (yalnız
  domain-içi yardımcı, yalnız domain-service veya başka aggregate tarafından kullanılan) kapsam dışıdır.
- **FR-001**: Handler'dan çağrılan, durum değiştiren aggregate davranış metodu `ResultDomain` (veri
  yoksa) veya `ResultDomain<T>` (veri varsa) döner; ham `bool`/`enum`/değer dönmez.
- **FR-002**: Handler'dan çağrılan aggregate fabrika metodu (`Create`/`Issue` vb.) `ResultDomain<T>`
  döner; başarıda `Ok(data)`, doğrulama ihlalinde `Error(messages)`.
- **FR-003**: Saf sorgu/getter metotları (property, `bool Is...`, `Get...` lookup, hesap) handler'dan
  çağrılsalar bile muaftır; ham değer dönebilir (durum değiştirmeyen okuma sonucu sarılmaz).
- **FR-004**: Outcome-enum dönen metotlar `ResultDomain<TEnum>.Ok(outcome)` ile sarılır; enum'un
  "başarısız" durumları `Error`'a eşlenmez (yorumu çağıran verir).
- **FR-005**: Refactor edilen her metodun tüm çağıranları (handler, MCP tool, diğer domain kullanımı)
  yeni `ResultDomain` sözleşmesine göre güncellenir; `IsSuccess`/`Data`/`Messages` deseni kullanılır.
- **FR-006**: Mevcut birim testleri güncellenir ve yeşil kalır; sözleşme değişen her metot için en az
  bir başarı ve bir başarısızlık senaryosu test edilir (test zaten varsa).
- **FR-007**: `Domains/` hemen altındaki her klasör tam olarak bir AggregateRoot içerir; iç içe
  aggregate yoktur. İhlal varsa aggregate kendi klasörüne taşınır (namespace + çağıranlar güncellenir).
- **FR-008**: Standalone value object'ler ilgili aggregate'in `ValueObjects/` alt klasörüne konur.
- **FR-009**: `SharedKernel/`, domain service, seeder ve MCP tool sınıfları aggregate-klasör kuralının
  kapsamı dışındadır ve yerinde kalır.
- **FR-010**: Üç kural (FR-001..FR-004 özeti, FR-007, FR-008) her iki repo'nun `CLAUDE.md`'sine
  örnekli ve muafiyetli olarak yazılır.
- **FR-011**: Standart hem PaymentGateway hem ECommerceAgent'te paralel uygulanır; her
  repo kendi spec/plan/tasks döngüsüyle ilerler ama kural metni aynıdır.

### Key Entities

- **ResultDomain / ResultDomain\<T\>**: Common'daki sonuç zarfı; `IsSuccess`, `Messages` (+ `Data`).
  Domain katmanının tek dönüş sözleşmesi.
- **Aggregate davranış metodu**: durum değiştiren veya fabrika metodu — sarma kapsamında.
- **Value Object**: aggregate'e ait değişmez değer tipi — `ValueObjects/` yerleşim kapsamında.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: İki repo'da da **handler'dan çağrılan** aggregate davranış/fabrika metotlarının %100'ü
  `ResultDomain`/`ResultDomain<T>` döner; handler'dan çağrılıp ham enum/bool/değer dönen davranış
  metodu sayısı 0 (saf getter muaf).
- **SC-002**: Her iki çözüm `dotnet build` 0 hata verir.
- **SC-003**: Mevcut domain birim test paketleri (`Merchant.Api.Tests`, `Commission.Api.Tests` ve
  ECommerce muadilleri) 0 başarısızlıkla geçer.
- **SC-004**: Her `Domains/<X>/` klasöründe en fazla bir AggregateRoot bulunur (iç içe aggregate = 0).
- **SC-005**: Her iki `CLAUDE.md` üç kuralı da içerir (metin denetimiyle doğrulanabilir).

## Assumptions

- **Kapsam call-site ile belirlenir (Karar 3 — onaylandı)**: yalnız handler'dan çağrılan aggregate
  davranış/fabrika metotları hedeflenir. Domain service (saf hesap, ör. `BankRouter`), MCP tool,
  seeder, read-model ve hiçbir handler'dan erişilmeyen iç metotlar muaftır.
- Getter/sorgu muafiyeti geçerlidir (Karar 1 — onaylandı): handler'dan çağrılsa bile saf okuma sarılmaz.
- Outcome-enum'lar `Ok(outcome)` ile sarılır, `Error` eşlemesi yapılmaz (Karar 2 — onaylandı).
- Fabrika muafiyeti yok: handler'dan çağrılan `Issue`/`Create` asla başarısız olmasa da tek-tiplik
  için `ResultDomain<T>.Ok(...)` sarılır.
- Aggregate-klasör kuralı PaymentGateway'de (Merchant BC) zaten uygulandı; kalan iş çoğunlukla
  ECommerce tarafında + iki repo'da sonuç-sarmalama.
- Her repo kendi spec numarasıyla ilerler: PaymentGateway `014`, ECommerce `031`; kural metni ortaktır.
- `SharedKernel/` klasörü aggregate-klasör kuralının bilinçli istisnasıdır ve korunur.
