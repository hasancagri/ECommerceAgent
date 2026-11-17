# Research — Sohbet Kayıt Sadakati + Kalıcı Sidebar

Kararlar tasarım belgesinde (`docs/superpowers/specs/2026-11-17-...`) çözüldü; burası özeti.
NEEDS CLARIFICATION kalmadı.

## D1 — Kart saklama: VO alanı mı, ayrı belge mi?

- **Karar:** `ChatMessage` VO'ya opak `string? ItemsJson` alanı (assistant turunda dolu).
- **Gerekçe:** Tam sadakat + aggregate sınırında kalır; anon zaten `StoredMsg.items` tutuyor (parite).
- **Elenen:** Ayrı yan-belge/event-log (fazla hareketli parça); tur başına ham blob (aggregate'i bulandırır).

## D2 — ItemsJson şekli: ham collector mı, unwrap-sonrası mı?

- **Karar:** `reply.Items`'ın **unwrap-sonrası** serileştirilmişi (`[{tool, result}]`, React `ReplyItem[]`).
- **Gerekçe:** `SendMessage.Unwrap/ToElement` MCP sarmalını zaten açıyor; React `ToolItem` bu şekli
  tüketiyor. Reload'ın birebir render'ı buna bağlı. Ham collector React'ta kırılır.

## D3 — Bağlam zehirlenmesi riski

- **Karar:** `BuildContext` dokunulmaz — yalnız `m.Text`, son `MaxContextMessages`, tur-başı `Clip`.
  Kartlar saklanır ama modele giden bağlama GİRMEZ.
- **Gerekçe:** Bağlam zaten metin-only + sınırlı; saklama ile okuma ayrı endişe. Maliyet sabit (SC-005).

## D4 — Rename sıralamayı etkiler mi?

- **Karar:** `Rename` `UpdatedTime`'a DOKUNMAZ (sidebar sırası son-aktiviteye göre; rename başa atmaz).
- **Gerekçe:** claude.ai davranışı; başlık düzeltmesi "etkinlik" değildir. Lenient LLM `SetTitle` ayrı kalır.

## D5 — Silme sertliği + açık-sohbet

- **Karar:** HARD delete (`session.Delete<Conversation>`), sahiplik guard, endpoint 204. Frontend:
  silinen aktifse pane yeni-sohbet durumuna sıfırlanır + `conversationId.ref` temizlenir.
- **Gerekçe:** 013 HARD-delete emsali. `SendMessage` zaten eksik/yabancı id'de yeni sohbet başlatıyor —
  ref temizlenmezse "mesajım yeni sohbet açtı" karmaşası olur.

## D6 — Sidebar nerede yaşar?

- **Karar:** Mevcut `PublicLayout` nav sidebar'ına gömülü; HER rota; satırlar link (`/sohbet?c=id`),
  aktif-vurgu URL param'dan. Gönder/aktif-state `ChatWorkspace`'te kalır.
- **Gerekçe:** claude.ai pariti (liste her yerde) ama büyük state refactor YOK (sidebar sadece liste+link).
- **Elenen:** Ayrı `ChatLayout` (orta yol, gereksiz); yalnız /sohbet (kalıcılık hissi vermiyor).

## D7 — Anon görünürlük

- **Karar:** Anon sohbetler sessionStorage'dan sidebar'da oturumluk görünür; rename/sil yerel
  (`renameAnon` eklenir, `deleteAnon` var). Sunucuya yazılmaz.
- **Gerekçe:** Login'le his pariti; anon doğası gereği geçici (sekme kapanınca gider).
