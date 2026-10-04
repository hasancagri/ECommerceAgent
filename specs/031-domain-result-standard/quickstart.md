# Quickstart / Doğrulama: Domain Sonuç Sarmalama (ECommerce)

Refactor davranışı değiştirmez; doğrulama derleyici + mevcut birim testleri.

## Ön koşul
- .NET 10 SDK
- Repo kökü: `/Users/macbook/Desktop/ECommerceAgent`

## Adımlar

1. **Build (tüm çözüm)**:
   ```bash
   dotnet build
   ```
   Beklenen: `0 Hata`. Sarılmamış void/ham call-site kalırsa CS hatası verir (istenen kapı).

2. **Domain birim testleri**:
   ```bash
   dotnet test tests/Basket.Api.Tests
   dotnet test tests/Stock.Api.Tests
   dotnet test tests/Payment.Api.Tests
   dotnet test tests/Catalog.Api.Tests
   dotnet test tests/Customer.Api.Tests
   ```
   Beklenen: 0 başarısız.

3. **Void/ham mutator taraması** (regresyon kapısı):
   ```bash
   grep -rnE "public void (AddItem|SetItem|SetStatus|Increase|Decrease|Update|StartReservation|PurgeExpiredItems)\(" \
     src/services --include=*.cs
   ```
   Beklenen: eşleşme yok (hepsi `ResultDomain`).

## Kabul (Success Criteria eşlemesi)

| Ölçüt | Doğrulama |
|-------|-----------|
| SC-001 (handler-çağrılı ham/void davranış = 0) | Adım 3 grep boş + 10 metot sarıldı |
| SC-002 (build 0 hata) | Adım 1 |
| SC-003 (testler yeşil) | Adım 2 (5 paket) |
| SC-004 (iç içe aggregate = 0) | 9 aggregate zaten kendi klasöründe |
| SC-005 (CLAUDE.md 3 kural) | `CLAUDE.md`'de 3 madde |
