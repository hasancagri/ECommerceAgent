# Quickstart — Taste-vektör kişiselleştirme (Dilim-4) doğrulama

Ön koşul: 025 embedding + 026 semantik canlı, `product_embedding` dolu. `scout-openai-key` set
(Personalization'a da env). Login kullanıcı + ≥3 arama (sinyal).

## Çalıştır

```bash
docker  # açık
dotnet run --project src/aspire/AppHost/AppHost.csproj
# React: ~/dev/ECommerceWeb npm run dev
```

## Senaryolar (US/SC)

### US1 / SC-001 — kişisel feed
1. Login ol, ≥3 anlamlı arama yap (ör. "karanlık distopya", "melankolik roman", "savaş romanı") — `UserSearchRecord` birikir.
2. "Sana özel" feed aç (ScoutAgent `GET /api/chat/v1/feed`, login token).
3. **Beklenen:** aramaların anlam-merkezine yakın kitaplar (distopya/melankoli/savaş bölgesi), rastgele değil;
   `tool: "recommend_books"`. Backend izole: feed ucu → kartlar.

### US1 / SC-003 — cold-start fallback
- Yeni/<3 sinyal kullanıcı feed → **küratölü** (çok-satan/popülerlik), boş DEĞİL.
- Anonim feed → 401 (kişisel login şart).

### US2 / SC-002 — spesifik sorgu kişiselliği ezer
- Feed yerine "Dostoyevski" ara → `search_books` (lexical); "karanlık roman" → semantic/lexical. Taste KARIŞMAZ, regresyon yok.

### SC-004 — çeşitlilik + tekrar yok
- Feed sonuçları eser başına tek (WorkKey) + dar tek kümeye sıkışmaz.

## gRPC + taste duman testi (elle)

```bash
# Personalization GetTaste — signal_count + vector uzunluğu (değeri loglama, İlke VIII)
# (grpcurl ya da Aspire dashboard; vector 1536 uzunluk + signal_count = distinct sorgu sayısı beklenir)
```
Taste merkezi psql ile dolaylı: kullanıcının sorgularını OpenAI'la embed→ortalama→`product_embedding` kNN,
komşular aranan temalarla ilgili olmalı (026 quickstart kNN SQL deseniyle).

## Birim testler (host'suz)

```bash
dotnet test tests/Personalization.Api.Tests/ --filter TasteVector   # Average + IsColdStart saf
dotnet test tests/Catalog.Api.Tests/ --filter Recommend             # küratörlü-eşik/çeşitlilik seçimi (saf kısım)
```

## Regresyon kalkanı
Mevcut arama yolları (lexical/semantik/benzer) + REST `/products/search` + 024 benzer akışı bozulmadı.
Taste-vektör hiçbir insan-yüzeyde görünmez (yalnız gRPC, sunucuda).
