using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ScoutAgent.Api.Options;
using ScoutAgent.Api.SubAgents;

namespace ScoutAgent.Api.Agents;

// 019: Router + BC-başına sub-agent (agent-as-tool). Router (ucuz model) niyeti anlar → sub-agent
// tool'larından birini (veya çok-alanlı istekte birkaçını) seçer. Sub-agent tool'ları LAZY
// (AIFunctionFactory): yalnız router o tool'u ÇAĞIRINCA ilgili MCP ucuna bağlanılır → seçilmeyen uç
// hiç yoklanmaz (SC-001). Her sub-agent kendi modeliyle (FR-004) + dar prompt'uyla çalışır. Leaf MCP
// tool'ları CapturingFunction ile sarılı → sonuç collector'a (reply.items). Agent Singleton kuralı:
// per-istek kurulan router, kullanıcı token'ı çağrı anında UserTokenForwardingHandler ile taşınır.
public sealed class ScoutAgentFactory(
    IEnumerable<ISubAgent> subAgents, ChatClientFactory clients, ScoutOptions options) : ISingletonDependency
{
    // Router agent'ı kur — her sub-agent kendi lazy tool'unu verir (bağlantı yok; invoke'ta bağlanır).
    // ToolName'e göre sıralı → reply.items + tool listesi deterministik (D5). collector leaf sonuçları toplar.
    public AIAgent CreateRouter(ToolResultCollector collector)
    {
        var tools = subAgents
            .OrderBy(s => s.ToolName, StringComparer.Ordinal)
            .Select(s => s.AsRouterTool(collector))
            .ToList();

        var routerClient = clients.ForModel(ScoutModelResolver.ResolveRouter(options));
        return new ChatClientAgent(routerClient, instructions: RouterPrompt, name: "KasifRouter", tools: tools);
    }

    // 014: sohbet için AKILLI başlık (ilk turdan; Claude-tarzı sidebar). 3-5 kelime, kitap-keşfi bağlamı.
    public async Task<string> GenerateTitleAsync(string firstUserMessage, string assistantReply, CancellationToken ct)
    {
        var response = await clients.ForModel(ScoutModelResolver.ResolveRouter(options)).GetResponseAsync(
            "Aşağıdaki sohbet turundan 3-5 KELİMELİK kısa, öz bir başlık üret (Türkçe, kitap-keşfi " +
            "bağlamı; ör. 'Dostoyevski ucuz kitaplar', 'Rus klasikleri'). SADECE başlığı yaz — tırnak/" +
            "nokta/açıklama YOK.\n\nKullanıcı: " + firstUserMessage + "\nKâşif: " + assistantReply,
            cancellationToken: ct);
        var title = (response.Text ?? string.Empty).Trim().Trim('"', '\'', '.', '—', '-').Trim();
        return title.Length > 60 ? title[..60].TrimEnd() + "…" : title;
    }

    // Router YALNIZ dağıtım yapar — işi sub-agent'lara devreder. Alan bilgisi sub-agent prompt'larında.
    private const string RouterPrompt =
        "Sen 'Kâşif'sin — bir kitap keşif asistanının yönlendiricisisin. Kullanıcının mesajındaki NİYETİ " +
        "anla ve uygun uzman aracı çağır. ⚠️ EN ÖNEMLİ: `request` parametresine kullanıcının mesajını " +
        "BİREBİR KOPYALA — Türkçe'den İNGİLİZCE'YE ÇEVİRME, özetleme, kelime ekleme/çıkarma YOK. Özel " +
        "adları (yayınevi, yazar, kitap adı: 'Can Yayınları', 'İş Bankası', 'Dostoyevski'...) TAM ve " +
        "DEĞİŞMEDEN koru — tek kelimesini bile DÜŞÜRME ('Can Yayınları' asla 'Yayınları' olmaz). " +
        "Çeviri/paraphrase sorguyu bozar, yanlış sonuç verir. KURALLAR: " +
        "(1) Kitap arama/detay/künye/fiyat/sayfa/yayınevi/sıralama/'devamını getir' → `catalog_agent`. " +
        "(2) Bir kitabın yorumları/puanı → `reviews_agent`. " +
        "(3) Kişisel: sepet → `basket_agent`, siparişler → `order_agent`, kitaplık → `library_agent`, " +
        "adresler → `customer_agent`, arama geçmişi → `personalization_agent`. " +
        "(4) Bir mesaj birden çok alanı kapsıyorsa (ör. 'sepetim ve Dostoyevski kitapları') İLGİLİ " +
        "araçların HEPSİNİ çağır. " +
        "(5) Konu kitap keşfi ya da kullanıcının kendi verisi DIŞINDAysa (ör. hava durumu) hiçbir araç " +
        "çağırma; kapsamını nazikçe söyle. " +
        "(6) Araç sonuçları kullanıcıya KART olarak gösteriliyor — sen yalnız 1-2 cümlelik KISA bir " +
        "giriş/özet yaz, kitapları/kartları metinde TEKRAR LİSTELEME. " +
        "(7) İç kimlikleri (GUID) kullanıcıya gösterme.";
}
