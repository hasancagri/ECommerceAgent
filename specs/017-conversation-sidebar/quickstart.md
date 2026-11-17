# Quickstart — Doğrulama (elle, Aspire tam-stack)

Ön koşul: `dotnet run --project src/aspire/AppHost/AppHost.csproj` + ECommerceWeb `npm run dev` (:5173).
scoutDb sohbetleri temizlendi (0 kayıt) — bu özelliğe temiz başlangıç.

## Birim (domain, otomatik)

```bash
dotnet test tests/ScoutAgent.Api.Tests/ScoutAgent.Api.Tests.csproj
```
Beklenen: `ConversationItemsTests` (kart saklama) + `ConversationRenameTests` (guard'lar) yeşil.

## US1 — Kayıt sadakati

1. Login ol, kart dönen bir soru sor ("en popüler romanlar").
2. Sohbeti kapat (başka rota), sidebar'dan tekrar aç.
3. **Beklenen:** assistant cevabı metin + aynı kitap kartlarıyla görünür (kart kaybı 0, SC-001).
4. Düz-metin turlu bir sohbeti aç → kart alanı yok, metin tam.

## US2 — Kalıcı sidebar

1. Login'le farklı rotalara git (kitaplık, sepet, hesap).
2. **Beklenen:** sol panelde sohbet listesi her ekranda; en son etkinlik üstte.
3. Bir sohbete tıkla → tam transkript (kartlar) yüklenir, kaldığın yerden yaz.
4. "Yeni sohbet" → ilk mesaj sonrası listede belirir, aktif olur.

## US3 — Rename / Sil

1. Bir sohbeti yeniden adlandır → yeni başlık listede; **sıra değişmez** (SC-003).
2. Boş başlıkla dene → reddedilir, eski başlık kalır.
3. Bir sohbeti sil → listeden ve DB'den kalkar. scoutDb teyit:
   ```bash
   # row sayısı düşmeli
   docker exec -e PGPASSWORD=$(docker exec <pg> printenv POSTGRES_PASSWORD) <pg> \
     psql -U postgres -d scoutDb -tAc "SELECT count(*) FROM scout.mt_doc_conversation;"
   ```
4. **Açık** sohbeti sil → pane yeni-sohbet durumuna döner; sonraki mesaj yeni sohbete gider (SC-004).
5. Başkasının id'siyle PATCH/DELETE → 404.

## US4 — Anon

1. Çıkış yap; iki sohbet aç → ikisi sidebar'da.
2. Anon sohbeti rename/sil → yalnız yerel; sunucuya yazılmaz.
3. Sekmeyi kapat-aç → anon sohbetler gitti (kalıcı değil).

## Bağlam guard (SC-005)

- Uzun sohbette yeni tur: modele giden bağlam yalnız metin + sınırlı; kart JSON'u bağlamda YOK
  (log/istek inceleme). Maliyet kart-saklama öncesiyle aynı aralıkta.
