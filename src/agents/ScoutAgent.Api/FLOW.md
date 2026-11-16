> Lejant: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟩 read-model · 🩷 dış sistem · 👤 aktör · 🟥 hotspot

# ScoutAgent (Kâşif) — sohbet süreci

ScoutAgent serbest metni alır, yerel modelle niyeti anlar, doğru BC MCP tool'unu çağırır, cevabı
sohbet olarak döner; girişli kullanıcının transkriptini kendi deposunda tutar.

## Süreç

1. 👤 Ziyaretçi/kullanıcı chat'e mesaj yazar → 🟦 `SendMessage` _(planned)_
2. 🟪 Girişliyse kayıt yüklenir, `[özet]+[son N]` bağlamı kurulur (`ConversationContext.Build`);
   anonimse oturumluk bağlam.
3. 🟪 Oturum durumuna göre araç kutusu süzülür (anonim = yalnız anonim katalog tool'ları).
4. 🩷 Yerel model (Ollama) tool seçer → 🩷 ilgili BC `/mcp` tool'u çağrılır (kullanıcı token'ı taşınır).
5. 🟧 Cevap `{text, items}` üretilir — `items` tool dönüşünden birebir (uydurma yok).
6. 🟪 Girişliyse user + assistant mesajı `Conversation`'a eklenir; eşik aşılınca eskiler özetlenir
   (🟦 `ApplySummary`). Anonim = kalıcı kayıt yok.

## Domain kuralları

- UserId doğumda sabit; Messages yalnız aggregate metotlarından büyür.
- Transkriptte YALNIZ metin durur — hacimli tool sonuçları bağlama ham gömülmez.
- Devamlılık ScoutAgent'ın KENDİ kaydından kurulur; agent çerçevesi state'ine güvenilmez.
- Kullanıcı yalnız KENDİ sohbetini görür/sürdürür.

## Sınır

- BC verisini DEĞİŞTİRMEZ (yalnız query-only MCP okur). Kendi tek kalıcı verisi = sohbet.
- Dış agent/DCR/consent yüzeyi YOK — yalnız first-party React tüketir.
