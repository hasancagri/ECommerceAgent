using System.Diagnostics;
using Npgsql;

namespace Storefront.Api.Domains.StorefrontView.Features.Agents.Queries;

// 069: tek serbest-sorgu kapısı. Akış (R2/R3): {{EMBED}} ikamesi → saf bekçi → (geçerse) embedding
// üretimi → kısıtlı rol bağlantısında statement_timeout ile çalıştırma → ret DAHİL her yolda
// AgentQueryLog (sahip oturum). Yapısal zırh kısıtlı roldedir (TEK yetki view SELECT'i, FR-005);
// bekçi ucuz ön-kapı + makine-okur ret kodudur (FR-007 düzeltme döngüsü).
public static class QueryStorefront
{
    public record QueryStorefrontQuery(string Sql);

    // R5: serbest SELECT listesi sabit DTO ile temsil edilemez — kolon-adlı satırlar döner.
    // vector kolonları AYIKLANIR (embedding asla LLM'e dönmez; token + sızıntı).
    public class QueryStorefrontResponse
    {
        public bool Ok { get; set; }
        public List<Dictionary<string, object?>> Rows { get; set; } = [];
        public int RowCount { get; set; }
        public bool Truncated { get; set; }
    }

    public class QueryStorefrontQueryHandler
    {
        public async Task<FeatureObjectResultModel<QueryStorefrontResponse>> Handle(
            QueryStorefrontQuery query,
            AgentQueryConnectionSource restricted,
            IDocumentStore store,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            AgentQueryOption options,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var rawSql = query.Sql ?? string.Empty;

            // 1) {{EMBED}} ikamesi (bekçiden ÖNCE — bekçi ikameli SQL'i görür, kontrat sırası).
            var substitution = EmbedPlaceholder.Substitute(rawSql);
            if (!substitution.IsSuccess)
                return await Reject(store, rawSql, substitution.Messages!, sw, ct);

            // 2) Saf bekçi: tek SELECT/WITH, yasak kelime, ilişki whitelist, uzunluk, LIMIT sarmalama.
            var guarded = AgentSqlGuard.Validate(substitution.Data!.Sql, options.MaxSqlLength, options.MaxRows);
            if (!guarded.IsSuccess)
                return await Reject(store, rawSql, guarded.Messages!, sw, ct);

            // 3) Embedding yalnız yer-tutucu varsa (reddedilecek sorguya OpenAI parası harcanmaz, R3).
            var vectorLiterals = new List<string>();
            if (substitution.Data.Texts.Count > 0)
            {
                try
                {
                    var embeddings = await embeddingGenerator.GenerateAsync(
                        substitution.Data.Texts, cancellationToken: ct);
                    vectorLiterals.AddRange(
                        embeddings.Select(e => EmbedPlaceholder.ToVectorLiteral(e.Vector.Span)));
                }
                catch (Exception ex)
                {
                    var message = new MessageItem
                    {
                        Code = StorefrontResourceConstants.STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE,
                        Property = "embedding service unavailable"
                    };
                    await WriteLog(store, AgentQueryLog.Failed(rawSql, message.Code!, ex.Message,
                        (int)sw.ElapsedMilliseconds), ct);
                    return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
                }
            }

            // 4) Kısıtlı bağlantıda çalıştır: SET LOCAL statement_timeout işlem-yerel (FR-004 süre tavanı).
            try
            {
                var response = await Execute(restricted, guarded.Data!.WrappedSql, vectorLiterals, options, ct);
                await WriteLog(store, AgentQueryLog.Executed(rawSql, response.RowCount, response.Truncated,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Ok(response);
            }
            catch (PostgresException pex) when (pex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                // Rol katmanı reddi: bekçiyi geçen view-dışı erişim burada İMKANSIZ kılınır (yapısal).
                var message = new MessageItem
                {
                    Code = StorefrontResourceConstants.AgentSqlPermissionDenied,
                    Property = FirstLine(pex.MessageText)
                };
                await WriteLog(store, AgentQueryLog.Rejected(rawSql, message.Code!, pex.Message,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
            }
            catch (PostgresException pex) when (pex.SqlState == PostgresErrorCodes.QueryCanceled)
            {
                var message = new MessageItem
                {
                    Code = StorefrontResourceConstants.AgentSqlTimeout,
                    Property = $"statement_timeout {options.TimeoutSeconds}s exceeded"
                };
                await WriteLog(store, AgentQueryLog.Failed(rawSql, message.Code!, pex.Message,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
            }
            catch (PostgresException pex)
            {
                // R5: asistana budanmış tek satır (SQLSTATE + mesaj) — tam metin yalnız izde.
                var message = new MessageItem
                {
                    Code = StorefrontResourceConstants.AgentSqlExecutionFailed,
                    Property = $"{pex.SqlState}: {FirstLine(pex.MessageText)}"
                };
                await WriteLog(store, AgentQueryLog.Failed(rawSql, message.Code!, pex.Message,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
            }
        }

        private static async Task<QueryStorefrontResponse> Execute(
            AgentQueryConnectionSource restricted,
            string wrappedSql,
            List<string> vectorLiterals,
            AgentQueryOption options,
            CancellationToken ct)
        {
            await using var conn = await restricted.DataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            await using (var timeoutCmd = conn.CreateCommand())
            {
                // Değer config int'i — literal enjeksiyonu güvenli (SET LOCAL parametre bind edemez).
                timeoutCmd.CommandText = $"set local statement_timeout = {options.TimeoutSeconds * 1000}";
                timeoutCmd.Transaction = tx;
                await timeoutCmd.ExecuteNonQueryAsync(ct);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = wrappedSql;
            cmd.Transaction = tx;
            for (var i = 0; i < vectorLiterals.Count; i++)
                cmd.Parameters.AddWithValue($"emb{i}", vectorLiterals[i]);

            var rows = new List<Dictionary<string, object?>>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        // vector kolonları yanıttan ayıklanır (R5).
                        if (reader.GetDataTypeName(i).Contains("vector", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var value = await reader.GetFieldValueAsync<object>(i, ct);
                        row[reader.GetName(i)] = value is DBNull ? null : value;
                    }
                    rows.Add(row);
                }
            }

            await tx.CommitAsync(ct);

            // Sarmalanmış LIMIT = MaxRows+1: fazla satır geldiyse toplam tavandan büyük (R4).
            var truncated = rows.Count > options.MaxRows;
            var rowCount = rows.Count;
            if (truncated)
                rows.RemoveRange(options.MaxRows, rows.Count - options.MaxRows);

            return new QueryStorefrontResponse
            {
                Ok = true,
                Rows = rows,
                RowCount = rowCount,
                Truncated = truncated
            };
        }

        private static async Task<FeatureObjectResultModel<QueryStorefrontResponse>> Reject(
            IDocumentStore store, string rawSql, List<MessageItem> messages, Stopwatch sw, CancellationToken ct)
        {
            var first = messages[0];
            await WriteLog(store, AgentQueryLog.Rejected(rawSql, first.Code!, first.Property,
                (int)sw.ElapsedMilliseconds), ct);
            return FeatureObjectResultModel<QueryStorefrontResponse>.Error(messages);
        }

        // R6: iz SAHİP oturumla yazılır (kısıtlı rol view dışına yazamaz — kanallar ayrık).
        private static async Task WriteLog(IDocumentStore store, AgentQueryLog log, CancellationToken ct)
        {
            await using var session = store.LightweightSession();
            session.Store(log);
            await session.SaveChangesAsync(ct);
        }

        private static string FirstLine(string text)
        {
            var newline = text.IndexOf('\n');
            return newline < 0 ? text : text[..newline];
        }
    }
}

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir (005 karari).
// 069: search_storefront_products + find_similar_books TAM IKAME ile silindi — tek kapi query_storefront.
// 070 US5/FR-013: 069 sorgu rehberinin (playbook) KANONIK EVI artik bu Description — dis agent'lar
// (Claude Desktop vb.) rehberi tool'un kendisinden ogrenir; ChatAgent prompt kopyasi DONDURULDU
// (071 sokumune dek paralel yasar, oraya dokunma). ChatAgent-ozgu persona satirlari (kural 8/9,
// sepete ekleme) BURAYA GIRMEZ — yalniz SORGU rehberi (R7). Sema drift guard'i bu dosyayi hedefler
// (check-agent-query-schema.sh); kolon adlari duz metin kalmali.
[McpServerToolType]
public static class QueryStorefrontMcpTool
{
    // Rehber ayri const'ta: Description attribute'u derleme sabiti ister; bloklar okunur kalsin.
    private const string SchemaBlock =
        "Kitap magazasi vitrininde SERBEST salt-okur SQL sorgusu calistirir (Postgres). " +
        "\"Kitap ara\", \"su temada kitap oner\", \"fiyati sundan az kitaplar\" gibi serbest vitrin sorgulari icin. " +
        "TEK ilişki: storefront_sellable (yalniz satistaki kitaplar). Kolonlar: " +
        "product_id uuid, name text (kitap adi), description text (aciklama), authors text[] " +
        "(yazar adlari), publisher text (yayinevi), category text (kategori), price numeric (TL), " +
        "stock int (NULL=bilinmiyor), rating_average numeric (NULL=hic puan yok), rating_count int, " +
        "specs jsonb ([{Attribute,Option}] ozellik ciftleri), family_code text (varyant ailesi; " +
        "NULL=ailesiz), image_url text (kapak), added_at timestamptz (YAKLASIK eklenis), " +
        "discount_pct int (aktif indirim yuzdesi; NULL=indirim yok/pencere disi), " +
        "discount_ends_at timestamptz (aktif indirim bitisi; NULL=indirim yok), " +
        "effective_price numeric (odenecek etkin fiyat: indirim aktifse indirimli, degilse price ile ayni), " +
        "embedding vector (anlamsal temsil; yanita donmez, yalniz <=> mesafesinde kullan). " +
        "KURALLAR: tek SELECT/WITH; baska iliski/yazma YASAK; sonuc 50 satirla sinirlanir. ";

    private const string PatternsBlock =
        "SORGU KALIPLARI: " +
        "(1) KATALOG INGILIZCE: kategori/tur adlari Ingilizcedir — kullanici Turkce soylerse " +
        "Ingilizce karsiligiyla ara ('kurgu/roman' → '%fiction%', 'fantastik' → '%fantasy%', " +
        "'bilim kurgu' → '%science%'); Turkce kelimeyle ILIKE aramasi BOS doner. " +
        "(2) Ad/kelime: name ILIKE '%dune%'. Yazar: EXISTS (SELECT 1 FROM unnest(authors) a WHERE " +
        "a ILIKE '%le guin%') — TAM AD YAZMA, en ayirt edici parcayi (genelde soyad) yaz. " +
        "Yayinevi/kategori de ILIKE ile. " +
        "(3) Ozellik/varyant: EXISTS (SELECT 1 FROM jsonb_array_elements(specs) s WHERE " +
        "s->>'Attribute' ILIKE '%cilt%' AND s->>'Option' ILIKE '%ciltli%'). 'Bunun ciltli hali var " +
        "mi' icin: family_code = (SELECT family_code FROM storefront_sellable WHERE product_id = 'X') " +
        "AND product_id <> 'X'. " +
        "(4) Istatistik/karsilastirma/uc deger: GROUP BY + COUNT/AVG/MIN/MAX, ORDER BY + LIMIT; " +
        "VEYA/HARIC tek sorguda OR/NOT ile — elle cok arama yapip birlestirme. " +
        "(5) Puan sarti: rating_average > 4 (NULL'lar kendiliginden elenir). " +
        "(6) SAYFALAMA/genis liste: 'tum X'leri listele' isteginde ONCE COUNT(*) ile toplami ogren, " +
        "sonra ilk sayfayi LIMIT 20 ile ver; toplami soyle ve devamini isteyip istemedigini sor — " +
        "devami = AYNI sorgu, sonraki OFFSET. Yanitta truncated=true ise ayni davranis (sonuc " +
        "kirpilmistir; 'hepsini gosterdim' deme). " +
        "(7) KAPAK GORSELI: kullanici kapak/gorsel isterse image_url kolonunu SELECT'e ekle ve " +
        "yanitinda TIKLANABILIR link olarak sun (markdown: [Kapak](url)); gorseli inline " +
        "gosteremeyebilirsin, link her zaman ver. ";

    private const string SemanticBlock =
        "TEMALI/ANLAMSAL arama: bulanik tema/ruh hali/konu ifadesini {{EMBED:\"tema metni\"}} " +
        "yer-tutucusuyla yaz (vektore sistem cevirir; icine fiyat/yazar gibi yapisal kisim YAZMA). " +
        "Kalip: WHERE embedding IS NOT NULL AND embedding <=> {{EMBED:\"kis temali surukleyici " +
        "bilim kurgu\"}} < 0.68 ORDER BY embedding <=> {{EMBED:\"kis temali surukleyici bilim " +
        "kurgu\"}} — yapisal kisitlar (fiyat/stok/kategori/haric) AYNI sorguda WHERE'e eklenir. " +
        "0.68 ustu mesafe ALAKASIZDIR; esigi asla gevsetme. " +
        "BENZERLIK ('buna benzer ne var'): embedding kolonunu SELECT listesine ASLA YAZMA (degeri " +
        "donmez); once product_id bul, sonra TEK sorguda alt-sorgu kalibi (yeni {{EMBED}} URETME): " +
        "SELECT name, price FROM storefront_sellable WHERE product_id <> 'X' AND embedding IS NOT " +
        "NULL AND embedding <=> (SELECT embedding FROM storefront_sellable WHERE product_id = 'X') " +
        "< 0.68 ORDER BY embedding <=> (SELECT embedding FROM storefront_sellable WHERE " +
        "product_id = 'X') LIMIT 8. Istenirse fiyat/stok kisiti da eklenir. ";

    private const string HonestyBlock =
        "DUZELTME: hata donerse (messages[].code + property ipucu) sorguyu duzeltip EN FAZLA 2 kez " +
        "yeniden dene; yine olmazsa bu sorunun su an yanitlanamadigini soyle — teknik ayrinti dokme. " +
        "DURUST VERI SINIRI: satis adedi/bestseller verisi vitrinde YOK — 'en cok satan' sorulursa " +
        "bu verinin tutulmadigini durustce soyle, asla uydurma. added_at YAKLASIKTIR (kayit " +
        "guncellenme zamani) — eklenis sorularinda yaklasikligi belirt. " +
        "GROUNDING: yaniti YALNIZ donen satirlardan kur. Bos sonuc = durust 'bulunamadi' (hata " +
        "degildir); asla satir/alan/deger uydurma, alakasiz oneri sunma.";

    [McpServerTool(Name = Shared.StorefrontTools.QueryStorefront)]
    [Description(SchemaBlock + PatternsBlock + SemanticBlock + HonestyBlock)]
    public static Task<FeatureObjectResultModel<QueryStorefront.QueryStorefrontResponse>> QueryStorefrontAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("storefront_sellable uzerinde TEK SELECT/WITH sorgusu; anlamsal metin {{EMBED:\"...\"}} ile")]
        string sql)
        => bus.InvokeAsync<FeatureObjectResultModel<QueryStorefront.QueryStorefrontResponse>>(
            new QueryStorefront.QueryStorefrontQuery(sql), ct);
}

// 067 NOT: kesif envanteri tool'lari (list_categories/authors/publishers) Catalog'dadir
// (envanter otoritesi = Catalog; Storefront = tek sorgu kapisi yuzeyi).
