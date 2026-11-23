using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019/018: katalog uzmanı — KENDİ LLM'i + model. TEK tool: search_books (LİSTE); girdisi elle, çıktısı
// bilinen tip (sniff/reflection yok). get_book (tekil) 018'de SÖKÜLDÜ — tekil kitap da search_books
// (tam ad → en-iyi eşleşme kartı); derin künye React detay sayfasında (kart tıkla), sohbette değil.
public sealed class CatalogSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "catalog_agent";

    private const string Description =
        "Kitap arama ve kitap künyesi/detayı: kitap adı, yazar, konu, fiyat/sayfa/yayınevi/cilt süzme, " +
        "sıralama, satışta/indirimli, 'devamını getir' sayfalama. Kitap keşfi hep buraya.";

    private const string Instructions =
        "Sen katalog uzmanısın. HER kitap-keşfi isteğinde SORMADAN `search_books` çağır — ad/yazar OLMASA " +
        "da (gezinme/öneri/popülerlik de arama): mesaj kitapla ilgiliyse tool'u MUTLAKA çağır, boş-elle " +
        "'bulamadım' DEME. Niyeti parametreye çevir: ad/yazar/konu → `query`; yoksa `query` BOŞ bırak + " +
        "filtre/sort kullan. SIRALAMAYI ZORUNLU EŞLE (fiyat/sıra ima eden her istekte `sort` MUTLAKA " +
        "gönder): 'ucuz/en ucuz/ucuzdan pahalıya/artan fiyat/ucuzdan' → sort=price_asc; 'pahalı/en pahalı/" +
        "pahalıdan ucuza/azalan fiyat' → sort=price_desc; 'en çok satan/popüler/gözde/çok satan' → " +
        "sort=popular; 'yeni/yeni çıkan/son çıkan' → sort=newest. 'indirimli' → onlyDiscounted; 'satışta/" +
        "stokta' → onlySalable. YAYINEVİNİ ZORUNLU EŞLE: kullanıcı yayınevi söylerse (ör. 'İş Kültür'," +
        "'İş Bankası','Can Yayınları','Yapı Kredi/YKY','İletişim','Everest') adı `publisher`'a GİT — query'ye " +
        "KOYMA; 'X hariç/dışında/X olmayan/X dışı' → excludePublisher. Yazar+yayınevi BİRLİKTE gelirse " +
        "(ör. 'Tolstoy'un İş Kültür kitapları') yazarı `query`'ye, yayınevini `publisher`'a AYIR. " +
        "TAKSONOMİYİ EŞLE (kullanıcının ruh hali/tür/konu/yaş niyetini TAM sözlük değerine çevir, " +
        "query'ye KOYMA): 'mizahi/komik/eğlenceli' → moods=['mizahi/absürt']; 'karanlık/kasvetli' → " +
        "moods=['karanlık']; 'hüzünlü/dokunaklı' → moods=['hüzünlü/melankolik']; 'gerilim/sürükleyici' → " +
        "moods=['gerilimli/sürükleyici']. 'roman/öykü/şiir/deneme' → genres. 'çocuk/7 yaş' → " +
        "audienceLevels (yaşa en yakın aralık). 'aşk/savaş/tarih/distopya/kimlik' konu → themes. " +
        "'stokta/mevcut' → onlyInStock. Birden çok uyarsa diziye hepsini koy. Tam karşılık YOKSA uydurma — " +
        "query serbest metnine bırak. 'ne anlatıyor/özetle' → ÖZET ANLATMA, kartı göster + detay sayfasına yönlendir. " +
        "Belirli bir kitabın detayı → `search_books` tam adıyla (en-iyi " +
        "eşleşme kartı; derin künye kartın detay sayfasında). 'devamını getir/sonraki' = SON aramanın " +
        "page+1'i. Sonuç GERÇEKTEN boşsa dürüstçe 'bulunamadı'. Sonuçlar kullanıcıya KART olarak gösterilir " +
        "— tek tek listeleme, 1-2 cümle giriş yaz. Fiyat/kapak/künye ASLA uydurma. İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "catalog")),
                    instructions: Instructions, name: ToolName,
                    tools: [SearchBooks(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    // search_books — girdi elle (13 alan), çıktı SearchBooksResult (LİSTE).
    private AITool SearchBooks(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string? query, int page, int pageSize, string? publisher, string? excludePublisher,
                   string? binding, decimal? minPrice, decimal? maxPrice, string? sort, bool onlySalable,
                   bool onlyDiscounted, int? minPageCount, int? maxPageCount,
                   string[]? genres, string[]? moods, string[]? audienceLevels, string[]? themes, bool onlyInStock,
                   CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("catalog", ct);
                var result = await client.CallToolAsync("search_books", new Dictionary<string, object?>
                {
                    ["query"] = query, ["page"] = page, ["pageSize"] = pageSize, ["publisher"] = publisher,
                    ["excludePublisher"] = excludePublisher, ["binding"] = binding, ["minPrice"] = minPrice,
                    ["maxPrice"] = maxPrice, ["sort"] = sort, ["onlySalable"] = onlySalable,
                    ["onlyDiscounted"] = onlyDiscounted, ["minPageCount"] = minPageCount,
                    ["maxPageCount"] = maxPageCount, ["genres"] = genres, ["moods"] = moods,
                    ["audienceLevels"] = audienceLevels, ["themes"] = themes, ["onlyInStock"] = onlyInStock,
                }, cancellationToken: ct);
                var typed = McpToolSource.DeserializeResult<SearchBooksResult>(result);
                collector.Add("search_books", typed);
                return typed;
            },
            name: "search_books",
            description: "Kitap adı/yazar + filtre/sıralama/taksonomi ile kitap arar; sayfalı liste döner. " +
                "publisher: YALNIZ bu yayınevi (kısmi ad, ör. 'İş Kültür'). excludePublisher: bu yayınevini hariç tut. " +
                "sort: price_asc/price_desc/newest/popular. binding: karton/ciltli. onlyInStock: yalnız stokta. " +
                "TAKSONOMİ (TAM sözlük değeri — çoklu): genres (roman/öykü/şiir/deneme/biyografi/çocuk resimli kitap...), " +
                "moods (karanlık/mizahi-absürt/hüzünlü-melankolik/gerilimli/romantik/ağır-düşündürücü...), " +
                "audienceLevels (çocuk (4-6)/çocuk (7-10)/genç yetişkin/yetişkin-orta...), " +
                "themes (aşk-romantizm/distopya-ütopya/savaş-barış/güç-iktidar/tarih-bellek/kimlik...). Sayfalama: page/pageSize.");
}
