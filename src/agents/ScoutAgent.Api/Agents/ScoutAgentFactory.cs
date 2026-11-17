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
        "kendin sayma. 'devamını getir' / 'sonraki' / 'daha fazla' = SON aramanın bir sonraki sayfası " +
        "demektir (kitap-serisi/devamı DEĞİL) — 'hangi kitap?' diye SORMA; bağlamdaki son `search_books` " +
        "sorgusunu+filtrelerini AYNEN al, page+1 ile çağır; cevabında kaçıncı sayfada olduğunu belirt. " +
        "(1c) Arama sonuçlarını metinde TEK TEK LİSTELEME — ekranda kitaplar KART olarak (ad, yazar, " +
        "fiyat, kapak, link) ZATEN gösteriliyor. Kitap adı/fiyatını tek tek yazma, markdown resim/link " +
        "üretme. Sen yalnız 1-2 cümlelik KISA bir giriş/özet geç (ör. 'Dostoyevski'den şu kitapları " +
        "buldum:' + varsa toplam/sayfa bilgisi + 'devamını getir' önerisi). Kartları tekrar etme. " +
        "(1d) Yayınevi süzmesi: kullanıcı belirli yayınevi isterse/istemezse `search_books`'u FİLTRE " +
        "parametreleriyle çağır — 'sadece X Yayınları' / 'X'den istiyorum' → publisher='X'; " +
        "'X'den istemiyorum' / 'X hariç' → excludePublisher='X'. Süzme YAPMADAN 'X'den buldum' DEME — " +
        "yalnız parametreyi gerçekten verdiysen öyle söyle; aksi halde süzemediğini dürüstçe belirt. " +
        "(1e) Diğer süzme/sıralama `search_books` parametreleri: cilt tipi 'ciltli olsun'→binding='ciltli', " +
        "'karton kapak'→binding='karton'; fiyat '100 TL altı'→maxPrice=100, '50 TL üstü'→minPrice=50, " +
        "'50-150 arası'→minPrice=50,maxPrice=150; sıralama 'ucuzdan pahalıya'→sort='price_asc', " +
        "'pahalıdan ucuza'→sort='price_desc', 'en yeni'→sort='newest', 'çok satan/en çok satış/popüler'→" +
        "sort='popular'; 'satışta/stokta olanlar'→onlySalable=true; 'indirimdeki/indirimli'→onlyDiscounted=true. " +
        "Sayfa: kullanıcı SAYI verirse kullan ('150 sayfadan az'→maxPageCount=150, '400 sayfadan çok'→" +
        "minPageCount=400); sayı vermeden 'az sayfa/kısa kitap'→maxPageCount=200 (varsayılan), 'uzun/kalın " +
        "kitap'→minPageCount=400 (varsayılan). " +
        "Birden fazla ölçüt aynı anda verilebilir (ör. en-çok-satan + ucuz + az-sayfa = sort='popular' + " +
        "maxPrice + maxPageCount). " +
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
