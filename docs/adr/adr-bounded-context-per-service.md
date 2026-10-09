---
status: Kabul
---

# ADR: Bounded Context = Mikroservis

> **Gerekçe/açıklama katmanı.** Bağlayıcı kural evi = `.specify/memory/constitution.md` **İLKE I** (BC
> İzolasyonu, NON-NEGOTIABLE) + `../../CLAUDE.md` + fiziksel klasör/DB ayrımı. Bu ADR kuralı KOPYALAMAZ —
> *neden* bu sınırı seçtiğimizi + alternatifleri anlatır; normatif metin İLKE I'dedir.

**Durum:** Kabul edildi (yürürlükte)

## Bağlam

DDD'de bir **bounded context**, içinde belirli bir ortak dilin (ubiquitous language) tutarlı olduğu sınırdır. Aynı kavram (ör. "Ürün") farklı context'te farklı modeldir: Catalog'da aggregate, Basket'te `BasketItem` entity'si, Storefront'ta read-model satırı. Soru: bu sınırları koda nasıl yansıtacağız — tek uygulamada mantıksal modüller mi, yoksa ayrı servisler mi?

## Karar

**Her bounded context = bir mikroservis.** Sınır fiziksel ve sert:

- Her servisin **kendi Postgres veritabanı** var (`catalogDb`, `basketDb`, …; `AppHost.cs`'te bağlanır).
- Her servisin **kendi Marten şeması** var (`SchemaConstants`).
- **Ortak (paylaşılan) domain modeli YOK.** Servisler asla DB/tablo/aggregate paylaşmaz.
- **Bir BC gerektiği kadar zengin aggregate root içerebilir** (anayasa v1.3.0, 016): ör. Catalog'da `Product`+`Category`+`Author`+`Publisher` (052 — kitap künyesi: `Brand` çok-yazar `Author` + tek `Publisher`'a bölündü); diğerlerinde çoğunlukla tek kök (`Basket`, `Order`, `Payment`, `ProductStock`). Anemik aggregate yasak; kökler birbirine **Id ile** referans verir.
- Context'ler arası iletişim **üç sanksiyonlu yolla**: (1) RabbitMQ integration event'leri (asenkron), (2) MCP tool çağrıları (senkron, agent için), (3) **tipli senkron gRPC** — sanksiyonlu **çok-kanallı** S2S taşıması (anayasa v1.2.0, 012; 074 genişletme: S2S REST→gRPC göçü). Çağıran karşının API'sine erişir, DB'sine değil; DB izolasyonu korunur, fail-closed. Bugünkü kanallar (`Shared/Protos/`): `basket_items` (Order → Basket sepet kalemleri, 039), `payment_intent` (Order → Payment hosted intent/link, 077), `address_query` (Order → Customer varsayılan adres, 077), `merchant_key` (Payment → Customer MerchantKey, 077). (Tarihsel: 012'nin tek kanalı Basket/Order → Stock rezervasyonuydu; 056 rezervasyonu söktü → o proto kaldırıldı.)
- **Bilinçli istisnalar** (aggregate'siz üyeler): `Storefront` composite read model'dir (003 — anemik projeksiyon, anayasa istisnası; **086: event-sourcing + Elasticsearch doc'a taşındı**, hâlâ anemik projeksiyon). (Tarihsel: `Supplier.Gateway` ACL + `IngestionAgent` 041'de söküldü; **Procurement BC** + `Supplier.Api` de **050 first-party pivot'ta tümüyle SÖKÜLDÜ** — mallar mağazanın, ürün girişi first-party import (051) + admin edit (058). Bugün tedarikçi/feed hattı YOK.)

Paylaşılabilen tek şey **bilinçli sözleşmeler**: `Shared.IntegrationEvents` (event kontratları), `Shared.RabbitMqConstants` (exchange/queue adları), `Shared.Payloads`.

## Sonuçlar

**Artı**
- Sert izolasyon: bir context'in modelini diğerine sızdıramazsın; derleme zamanında ayrı proje/DB olduğu için kaza eseri kuplaj oluşmaz.
- Her context bağımsız evrilir; ubiquitous language karışmaz.
- Ölçekleme/deploy servis bazında.

**Eksi / bedel**
- Servisler arası her akış bir event ya da MCP çağrısı gerektirir; doğrudan join yok → veri bazen çoğaltılır (ör. `ProductId` + `ProductName` OrderItem'da denormalize).
- Nihai tutarlılık (eventual consistency); akışın yarım kalması mümkün (bkz. todo-catalog-picture-handler (silindi)).
- Payment gibi servislerin nasıl konuştuğu ayrıca belgelenmeli.

## Alternatifler (reddedildi)

- **Modüler monolit / paylaşılan DB** — sınır yumuşar, "Ürün" gibi kavramlar zamanla tek modele erir; DDD context ayrımı erozyona uğrar.
- **Ortak domain kütüphanesi** — tüm context'ler aynı `Product` tipini paylaşırsa bağımsız evrim biter; bilinçle reddedildi (yalnızca `Shared.*` sözleşmeleri paylaşılır).
