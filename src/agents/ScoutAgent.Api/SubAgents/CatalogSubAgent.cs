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
        "'bulamadım' DEME. ⚠️ EN ÖNEMLİ KURAL — kitapla ilgili HER istekte `search_books`'u MUTLAKA çağır ve " +
        "`query`'ye kullanıcının isteğini NEREDEYSE OLDUĞU GİBİ yaz: kitap adı/yazar + tür/ruh-hali/konu/yaş " +
        "kelimeleri (mizahi, karanlık, distopya, öykü, roman, çocuk, aşk, savaş...). SİSTEM bu kelimeleri " +
        "otomatik facet'e çevirir — sen facet/sözlük düşünme, sadece kelimeleri query'ye koy. Yalnız nezaket/" +
        "dolgu sözcüklerini (lütfen, acaba, misin, istiyorum) atabilirsin. Ör. 'kısa hikayelerden oluşan mizah " +
        "kitabı arıyorum' → query='mizah kısa hikaye'. SIRALAMAYI ZORUNLU EŞLE (fiyat/sıra ima eden her istekte `sort` MUTLAKA " +
        "gönder): 'ucuz/en ucuz/ucuzdan pahalıya/artan fiyat/ucuzdan' → sort=price_asc; 'pahalı/en pahalı/" +
        "pahalıdan ucuza/azalan fiyat' → sort=price_desc; 'en çok satan/popüler/gözde/çok satan' → " +
        "sort=popular; 'yeni/yeni çıkan/son çıkan' → sort=newest. 'indirimli' → onlyDiscounted; 'satışta/" +
        "stokta' → onlySalable. YAYINEVİNİ ZORUNLU EŞLE: kullanıcı yayınevi söylerse (ör. 'İş Kültür'," +
        "'İş Bankası','Can Yayınları','Yapı Kredi/YKY','İletişim','Everest') adı `publisher`'a GİT — query'ye " +
        "KOYMA; 'X hariç/dışında/X olmayan/X dışı' → excludePublisher. Yazar+yayınevi BİRLİKTE gelirse " +
        "(ör. 'Tolstoy'un İş Kültür kitapları') yazarı `query`'ye, yayınevini `publisher`'a AYIR. " +
        "'stokta/mevcut' → onlyInStock. (Tür/ruh-hali/konu/yaş kelimelerini facet'e ÇEVİRME — query'ye " +
        "koy, sistem çevirir.) BENZER KİTAP: 'şuna benzer/buna benzeyen/gibi başka/tarzında' isteğinde " +
        "ÖNCE `search_books` ile o kitabı bul (productId al), SONRA `similar_books(productId)` çağır — " +
        "sonuçta aynı eserin başka baskıları gelmez, her eser bir kez. " +
        "'ne anlatıyor/özetle' → ÖZET ANLATMA, kartı göster + detay sayfasına yönlendir. " +
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
                    tools: [SearchBooks(collector), SimilarBooks(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    // search_books — TEK string param (JSON). MAF/UseFunctionInvocation'ın çok-alanlı tipli şema
    // binding'i KIRILGAN (publisher/price_asc/fiyat alanlarını model gönderince gövdeye hiç girilmiyordu,
    // sessiz "sorun oluştu"). Tek string param binding'i her zaman sağlam → şemayı baypas edip JSON'u
    // SUNUCUDA defansif parse ediyoruz (bozuk/çıplak metin → query fallback). query-only zaten çalışıyordu.
    private AITool SearchBooks(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string filters, CancellationToken ct) =>
            {
                var args = ParseSearchFilters(filters);
                var client = await mcp.GetClientAsync("catalog", ct);
                var result = await client.CallToolAsync("search_books", args, cancellationToken: ct);
                var typed = McpToolSource.DeserializeResult<SearchBooksResult>(result);
                collector.Add("search_books", typed, args); // args echo → React sayfalama için saklar
                return typed;
            },
            name: "search_books",
            description: "Kitap arar. Tek argüman `filters`: JSON nesnesi (string). Alanlar (hepsi opsiyonel): " +
                "query (kullanıcının isteği OLDUĞU GİBİ — kitap adı/yazar + tür/ruh-hali/konu/yaş kelimeleri: " +
                "mizahi, karanlık, distopya, öykü, roman, çocuk, aşk — sistem facet'e çevirir), " +
                "publisher (yalnız bu yayınevi), excludePublisher (hariç), binding ('karton'/'ciltli'), " +
                "minPrice, maxPrice (TL sayı), sort ('price_asc'/'price_desc'/'newest'/'popular'), " +
                "onlySalable, onlyDiscounted, onlyInStock (bool), minPageCount, maxPageCount (sayı), " +
                "page, pageSize. Örn: {\"query\":\"roman\",\"publisher\":\"Can Yayınları\",\"sort\":\"price_asc\"} " +
                "ya da sadece {\"query\":\"mizahi öykü\"}.");

    // Model'in yolladığı JSON string'i gevşek parse eder (``` çiti temizle, tip toleranslı, bozuksa
    // tüm metni query say). Dönen dict doğrudan MCP search_books argümanları.
    private static Dictionary<string, object?> ParseSearchFilters(string? filters)
    {
        var args = new Dictionary<string, object?> { ["page"] = 1, ["pageSize"] = 10 };
        if (string.IsNullOrWhiteSpace(filters)) return args;

        var text = filters.Trim();
        if (text.StartsWith("```"))
            text = text.Trim('`').TrimStart('j', 's', 'o', 'n', 'J', 'S', 'O', 'N').Trim();

        if (!text.StartsWith('{'))
        {
            args["query"] = text; // çıplak metin → query
            return args;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                object? val = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt32(out var i) ? i : p.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null,
                };
                if (val is not null) args[p.Name] = val;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            args["query"] = text; // bozuk JSON → tüm metni query say
        }
        return args;
    }

    // similar_books — girdi productId + count, çıktı SimilarBooksResult (LİSTE; eser tekilleştirilmiş).
    private AITool SimilarBooks(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (Guid productId, int count, CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("catalog", ct);
                var result = await client.CallToolAsync("similar_books", new Dictionary<string, object?>
                {
                    ["productId"] = productId, ["count"] = count,
                }, cancellationToken: ct);
                var typed = McpToolSource.DeserializeResult<SimilarBooksResult>(result);
                collector.Add("similar_books", typed);
                return typed;
            },
            name: "similar_books",
            description: "Bir kitaba BENZER kitapları döner (konu/ton örtüşmesi + aynı yazar/tür). " +
                "productId: search_books sonucundaki kitabın GUID'i. count: kaç benzer (varsayılan 5, en çok 20). " +
                "Aynı eserin diğer baskıları gelmez, stokta olanlar önce.");
}
