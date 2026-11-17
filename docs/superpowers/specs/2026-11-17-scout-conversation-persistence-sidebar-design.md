# Tasarım — ScoutAgent Sohbet Kayıt Sadakati + Kalıcı Sidebar

**Tarih:** 2026-11-17 · **Durum:** onay bekliyor · **Boyut:** proje

## Niyet ve neden

Kullanıcının Kâşif ile yazışması **başından sonuna**, gördüğü haliyle (metin + kitap
kartları) kalıcı olsun; geçmiş sohbetler claude.ai gibi **kalıcı sol sidebar**'da yaşasın —
tıkla-aç-devam et, etiketini değiştir, çöpe at.

**Neden şimdi:** İki boşluk var. (1) Login sohbette tam metin transkripti DB'de ama tool
sonuçları (kartlar) saklanmıyor — sohbeti tekrar açınca kartlar kayboluyor. (2) Geçmiş ayrı
bir sayfada (`/sohbet-gecmisi`), kalıcı değil; "sohbet açılıp orada yaşıyor" hissi yok.

## Mevcut durum (as-built)

- **Login kaydı var:** `Conversation` aggregate (scoutDb). Özetleme kapalı — tam transkript
  taşınıyor (`SendMessage` satır 108). `ChatMessage` VO **yalnız** `Role+Text` tutuyor.
- **Kartlar saklanmıyor:** `reply.Items` bilinçli olarak transkripte girmiyor (VO yorumu:
  bağlam zehirlenmesini önler). `GetConversation` yalnız metin döndürüyor.
- **Bağlam kapalı + sınırlı:** `BuildContext` yalnız `m.Text` kullanıyor, son
  `MaxContextMessages` mesaj + tur-başı `Clip`. Zehirlenme riski zaten yapısal olarak düşük.
- **Yüzey:** `ListConversations` (id/title/updatedAt), `GetConversation`, `SendMessage`.
  **Rename yok** (yalnız LLM `SetTitle`), **sil yok**.
- **Frontend:** `/sohbet-gecmisi` (`ChatHistory.tsx`) iki bölüm: *Sohbetlerin* + *Aramaların*
  (013 arama geçmişi, ayrı özellik). `ChatWorkspace` login'de `sendMessage`+token (kalıcı).
  `anonChats.ts` sessionStorage: `deleteAnon` + `StoredMsg.items` var (anon kartları zaten tutuyor).

## Hedefler

- Login assistant mesajı kartlarıyla saklansın; reload'da aynen render edilsin.
- Kalıcı sol sidebar: sohbet listesi + yeni-sohbet + aktif-vurgu + her satırda aç/rename/sil.
- Login = DB otoriter; anon = sessionStorage (oturumluk), sidebar'da **birlikte** görünür.
- Eski `/sohbet-gecmisi` kalkar; 013 "Aramaların" **Hesap & Ayarlar**'a taşınır.

## Non-goal / anti-goal

- **Anti-goal:** Tool sonuçlarını LLM bağlamına beslemek (zehirlenme). Bağlam metin-only kalır.
- **Non-goal:** Pin, klasör/proje gruplama, sidebar-içi arama, tarih-grup zorunluluğu.
- **Non-goal:** Anon sohbetin DB'ye kaydı. Özetlemenin geri gelmesi.
- **Non-goal:** 013 personalization **backend**'ine dokunmak (yalnız frontend yuvası taşınır).

## Kısıtlar

- VSA + CQRS + Result pattern (conventions). Yetki: `RequireAuthorization` + sahiplik;
  013 deseni — kişisel veri, named-scope YOK, kimlik JWT `sub`'tan.
- **Marten record-VO tuzağı:** saklanan kart verisi **opak string (`ItemsJson`)** — stored VO'da
  `JsonElement`/parametreli-ctor yok (deserialize 500 riski). React parse eder.
- Marten belge-store: yeni alan şema değişikliği gerektirmez (otomatik).
- Wolverine keşfi: yeni handler'lar `*Handler` ile biter (taranır); endpoint `IMessageBus.InvokeAsync`.

## Yaklaşımlar

### Kayıt sadakati

- **A (seçili):** Kartları assistant `ChatMessage`'a `ItemsJson` olarak sakla; bağlam metin-only
  kalır. Tam sadakat + zehirlenme yok. Anon zaten böyle (parite).
- **B (elendi):** Ayrı yan-belge/event-log tool sonuçları. Aynı etki, fazla hareketli parça.
- **C (elendi):** Tur başına ham `{text,items}` blob. Aggregate'i bulandırır, React tipi zayıflar.

### Sidebar UX

- **A (seçili):** Uygulama kabuğunda **kalıcı sol kolon** (Claude). Sohbet her an erişilir,
  orada yaşar. Kullanıcının referansı bu.
- **B (elendi):** Ayrı geçmiş sayfası kalsın. İstenen "kalıcı" hissi vermiyor.
- **C (elendi):** Aç-kapa drawer/overlay. Claude kalıcı kolon; overlay his olarak farklı.

## Kararlaştırılan tasarım

### Backend (ScoutAgent.Api)

1. **`ChatMessage` VO:** opsiyonel `string? ItemsJson` eklenir (private setter, record-VO deseni
   korunur). `Create(role, text, createdAt, itemsJson?)`. **`ItemsJson` = `reply.Items`'ın
   unwrap-sonrası serileştirilmişi** (`[{tool, result}]`, React `ReplyItem[]` ile birebir) — ham
   collector çıktısı DEĞİL. Reload sadakati buna bağlı.
2. **`Conversation.AddAssistantMessage(text, itemsJson?)`:** kartları mesaja yazar. Test-first.
3. **`Conversation.Rename(title)`:** kullanıcı-tetikli; boş→hata, trim, üst-sınır (builder: ~100).
   **`UpdatedTime`'a DOKUNMAZ** — rename sohbeti sidebar'da başa atmaz (claude.ai gibi; sıra
   son-aktiviteye göre kalır). LLM için lenient `SetTitle` ayrı kalır (boş gelirse dokunmaz).
   Test-first (guard'lar + UpdatedTime değişmez).
4. **Sil:** handler sahiplik kontrolü sonrası `session.Delete<Conversation>(id)` — HARD (013).
   Endpoint **204** döner (gövdesiz). Frontend: silinen **aktif** sohbetse pane yeni-sohbet
   durumuna sıfırlanır + `conversationId.ref` temizlenir ("mesajım yeni sohbet başlattı" karmaşası
   olmaz); aktif değilse yalnız satır düşer.
5. **Yeni slice'lar** (`Domains/Conversations/Features/Commands`): `RenameConversation`,
   `DeleteConversation`. Endpoint: `PATCH /conversations/{id}` (body: title) + `DELETE
   /conversations/{id}`; ikisi `RequireAuthorization` + sahiplik (başkasınınki 404/forbid).
6. **`SendMessage`:** `reply.Items` serialize edilip `AddAssistantMessage`'a verilir. `BuildContext`
   **dokunulmaz** (metin-only, sınırlı). Anti-goal korunur.
7. **`GetConversation`:** `MessageView.ItemsJson` eklenir (assistant mesajlarında dolu).

### Frontend (ECommerceWeb)

8. **Kalıcı sidebar — mevcut `PublicLayout` nav sidebar'ına gömülür** (ayrı layout YOK): nav
   altına sohbet listesi + yeni-sohbet düğmesi. **HER rotada** görünür (kitaplık/sepet/hesap dahil,
   claude.ai gibi). Satırlar **link** (`/sohbet?c=id`), aktif-vurgu **URL param**'dan; **gönder/aktif
   sohbet state'i `ChatWorkspace`'te KALIR** (sidebar yalnız liste+link → büyük state refactor YOK).
   Her satırda "..." menü (rename inline/modal — builder; sil → onay). Login: `listConversations`.
   Anon: `listAnon` (sessionStorage). `ChatWorkspace`'in kendi iç sidebar'ı (satır 356-375) kaldırılır.
9. **Reload sadakati:** `ConversationDetail.messages[].items` eklenir; `ChatWorkspace` assistant
   kartlarını mevcut renderer ile çizer (anon zaten `StoredMsg.items`).
10. **Anon yardımcıları:** `renameAnon` eklenir (`deleteAnon` var). Sidebar aksiyonları login'de
    REST, anon'da local store üzerinde çalışır.
11. **Eski sayfa:** `/sohbet-gecmisi` **sert kaldırılır** (redirect yok — yerel app); `PublicLayout`
    nav linki güncellenir (ölü link bırakma). *Sohbetlerin* bölümü sidebar'a göçer; *Aramaların*
    (013) canlı mantığı (sayfalama/sil/temizle + token) `AccountSettings`'e taşınır (`AccountSettings`
    şu an mock sayfa — bu bölüm canlıya bağlanır; 013 API aynı).
12. **API istemcisi:** `renameConversation(id, title, token)` + `deleteConversation(id, token)`
    eklenir (`src/api/chat.ts`).

### Test (İlke VI — domain test-first)

- `AddAssistantMessage_WithItems_StoresItems` / `_WithoutItems_ItemsNull`.
- `Rename_EmptyTitle_ReturnsError` / `_Trims` / `_TooLong_ReturnsError` / `_SetsTitle`.
- (Sil/sahiplik handler düzeyi — domain testi değil; canlı/elle doğrulanır.)

## Builder'a kalanlar

- Sidebar görseli: tarih-grup (Bugün/Dün…) açık mı, düz-recency mi; mobil aç-kapa davranışı.
- Rename: inline-edit mi modal mı.
- Başlık üst-sınır karakter değeri (~100 öneri).
- *Aramaların* bölümünün `AccountSettings` içindeki yerleşimi (sekme/bölüm).