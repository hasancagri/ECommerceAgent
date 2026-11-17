using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ScoutAgent.Api.Agents;

// 007: Kâşif agent'ı — Singleton IChatClient (OpenAI gpt-4o-mini). Agent per-istek
// verilen araç kutusuyla kurulur (agent Singleton kuralının token muadili: durum çağrı anında enjekte).
public sealed class ScoutAgentFactory(ScoutOptions options) : ISingletonDependency
{
    private readonly IChatClient _chatClient = BuildChatClient(options);

    public AIAgent CreateAgent(IList<AITool> tools) =>
        new ChatClientAgent(_chatClient, instructions: SystemPrompt, name: "Kasif", tools: tools);

    // 007 US4/FR-011: rolling summary — eskiyen mesajları tek özet bloğuna damıtır (araçsız, düz model).
    public async Task<string> SummarizeAsync(string transcript, CancellationToken ct)
    {
        var response = await _chatClient.GetResponseAsync(
            "Aşağıdaki sohbeti 3-4 cümlede Türkçe özetle; adı geçen kitap/yazar/konuları ve kullanıcının " +
            "tercihlerini KORU (sonraki cevaplarda gerekecek):\n\n" + transcript,
            cancellationToken: ct);
        return response.Text ?? string.Empty;
    }

    private static IChatClient BuildChatClient(ScoutOptions options)
    {
        // OpenAI ChatClient → IChatClient (Microsoft.Extensions.AI.OpenAI).
        // UseFunctionInvocation ŞART: modelin tool_call'ını OTOMATİK yürütür (MCP tool'u çağır, sonucu
        // modele geri besle, nihai cevap). Onsuz tool çalışmaz (items boş + çöp çıktı).
        IChatClient client = new OpenAIClient(new ApiKeyCredential(options.ApiKey))
            .GetChatClient(options.Model)
            .AsIChatClient();
        return client.AsBuilder().UseFunctionInvocation().Build();
    }

    private const string SystemPrompt =
        "Sen 'Kâşif'sin — bir kitap keşif asistanı. Kullanıcının serbest metnini anlar, uygun aracı " +
        "çağırır ve sonucu sıcak, kısa bir sohbet diliyle Türkçe anlatırsın. KURALLAR: " +
        "(1) Bir kitap adı, yazar adı ya da konu geçtiğinde KULLANICIYA SORU SORMADAN hemen `search_books` " +
        "aracını çağır (ör. 'Nutuk katalogda var mı?' → search_books query='Nutuk'). Netleştirme sorma — " +
        "önce ara, sonuç boşsa dürüstçe 'bulunamadı' de. Belirli bir kitabın detayı istenirse `get_book` kullan. " +
        "(1b) Arama sonucu sayfalıysa (toplam, sonuç sayısından çoksa) cevabında kaçıncı sayfada " +
        "olduğunu BELİRT (ör. 'ilk 20 sonuç — 1. sayfa'); toplam sayıyı araç dönüşündeki değerden ver, " +
        "kendin sayma. Kullanıcı 'devamını getir' / 'sonraki' derse AYNI sorguyu bir sonraki sayfayla " +
        "(page+1) `search_books` çağır; önceki sayfayı tekrar isteme. " +
        "(2) Kitap/adres/sipariş/kitaplık bilgisini YALNIZ araç dönüşünden aktar — fiyat, kapak, künye " +
        "ASLA uydurma. " +
        "(3) Konu kitap keşfi ve kullanıcının kendi verisi dışındaysa (ör. hava durumu) kapsamını nazikçe " +
        "söyle, araç çağırma. " +
        "(3a) Kullanıcı kişisel verisini sorar (sepet, siparişler, kitaplık, adresler) ama ilgili araç " +
        "kutunda YOKSA, bu giriş gerektirdiği içindir — nazikçe giriş yapmasını iste, uydurma/çöp cevap " +
        "verme. Arama geçmişini SİLME isteğinde (araç yok) geçmiş ekranından silebileceğini söyle. " +
        "(4) İç kimlikleri (GUID) kullanıcıya gösterme. " +
        "(5) Uzun listeleri kısa tut; ayrıntı için kullanıcıyı davet et.";
}
