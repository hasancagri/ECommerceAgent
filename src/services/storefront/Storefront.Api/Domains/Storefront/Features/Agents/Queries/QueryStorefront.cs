
namespace Storefront.Api.Domains.Storefront.Features.Agents.Queries;

// 086: tek serbest-sorgu kapısı — artık ham Elasticsearch Query DSL (JSON) çalıştırır (SQL değil).
// Akış (R7): {{EMBED}} ikamesi (embedding üret → knn.query_vector'a bas) → minimal sunucu rail
// (sabit index + size≤50 clamp + _source whitelist + timeout) → ES'e low-level SearchAsync → ret
// DAHİL her yolda AgentQueryLog. Tam JSON guard KAPSAM DIŞI (R7); rail ucuz felaketi önler.
public static class QueryStorefront
{
    private const int MaxRows = 50;
    private const int SearchTimeoutSeconds = 5;

    // {{EMBED:"metin"}} — kaçışlı tırnak destekli; marker BÜYÜK harf (kontrat). ES DSL'de knn.query_vector
    // değeri olarak JSON float dizisine çevrilir.
    private static readonly Regex PlaceholderRegex = new(
        """\{\{EMBED:"((?:[^"\\]|\\.)*)"\}\}""", RegexOptions.Compiled);

    private static readonly Regex MarkerLeftoverRegex = new(
        @"\{\{\s*embed", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public record QueryStorefrontQuery(string Query);

    // ham ES _source satırları (kolon-adlı); embedding ASLA dönmez (_source whitelist rail).
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
            ElasticsearchClient client,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IDocumentStore store,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var rawQuery = query.Query ?? string.Empty;

            // 1) {{EMBED}} ikamesi — embedding üret → DSL'e JSON vektör dizisi bas (çalıştırmadan ÖNCE).
            var substitution = await SubstituteEmbeddingsAsync(rawQuery, embeddingGenerator, ct);
            if (!substitution.IsSuccess)
            {
                // embedding servisi hatası = Failed (çalışırken); bozuk placeholder = Rejected (çalışmadan).
                var isServiceError = substitution.Code == StorefrontResourceConstants.STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE;
                await WriteLog(store, isServiceError
                    ? AgentQueryLog.Failed(rawQuery, substitution.Code!, substitution.Detail, (int)sw.ElapsedMilliseconds)
                    : AgentQueryLog.Rejected(rawQuery, substitution.Code!, substitution.Detail, (int)sw.ElapsedMilliseconds), ct);
                return Error(substitution.Code!, substitution.Detail);
            }

            // 2) Minimal rail: JSON parse (bozuksa ret) + size≤50 clamp + _source whitelist zorla.
            JsonObject body;
            try
            {
                body = JsonNode.Parse(substitution.Json!)?.AsObject()
                       ?? throw new System.Text.Json.JsonException("sorgu gövdesi JSON nesnesi değil");
            }
            catch (System.Text.Json.JsonException jex)
            {
                await WriteLog(store, AgentQueryLog.Rejected(rawQuery,
                    StorefrontResourceConstants.STOREFRONT_SEARCH_BAD_QUERY, FirstLine(jex.Message),
                    (int)sw.ElapsedMilliseconds), ct);
                return Error(StorefrontResourceConstants.STOREFRONT_SEARCH_BAD_QUERY, "geçersiz JSON sorgu gövdesi");
            }

            var requestedSize = body["size"]?.GetValue<int?>();
            var effectiveSize = Math.Min(requestedSize ?? 10, MaxRows);
            body["size"] = effectiveSize;
            if (body["knn"] is JsonObject knn && knn["k"] is not null)
                knn["k"] = Math.Min(knn["k"]!.GetValue<int>(), MaxRows);
            // _source whitelist: embedding asla dönmez; alan kümesi sunucu-sabit.
            body["_source"] = new JsonArray(StorefrontSearchIndex.SourceFields.Select(f => (JsonNode)f!).ToArray());

            // 3) Çalıştır: low-level POST /{index}/_search + timeout (sonsuz sorgu yok).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(SearchTimeoutSeconds));

            StringResponse response;
            try
            {
                response = await client.Transport.RequestAsync<StringResponse>(
                    new EndpointPath(Elastic.Transport.HttpMethod.POST, $"/{StorefrontSearchIndex.IndexName}/_search"),
                    PostData.String(body.ToJsonString()), null, null, cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                await WriteLog(store, AgentQueryLog.Failed(rawQuery,
                    StorefrontResourceConstants.STOREFRONT_SEARCH_TIMEOUT, $"{SearchTimeoutSeconds}s aşıldı",
                    (int)sw.ElapsedMilliseconds), ct);
                return Error(StorefrontResourceConstants.STOREFRONT_SEARCH_TIMEOUT, $"arama {SearchTimeoutSeconds}s içinde bitmedi");
            }

            if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            {
                // status yoksa (null) bağlantı/timeout; varsa ES 4xx/5xx (bozuk DSL dahil) = çalıştırma hatası.
                var status = response.ApiCallDetails.HttpStatusCode;
                var code = status is null
                    ? StorefrontResourceConstants.STOREFRONT_SEARCH_TIMEOUT
                    : StorefrontResourceConstants.STOREFRONT_SEARCH_EXECUTION_FAILED;
                await WriteLog(store, AgentQueryLog.Failed(rawQuery, code,
                    FirstLine(response.ApiCallDetails.DebugInformation), (int)sw.ElapsedMilliseconds), ct);
                return Error(code, status is null ? "arama çalıştırılamadı" : $"ES hata {status}");
            }

            // 4) Yanıt ayrıştır — hits._source satırları (embedding zaten _source'ta yok).
            var result = ParseHits(response.Body, requestedSize);
            await WriteLog(store, AgentQueryLog.Executed(rawQuery, result.RowCount, result.Truncated,
                (int)sw.ElapsedMilliseconds), ct);
            return FeatureObjectResultModel<QueryStorefrontResponse>.Ok(result);
        }

        // ── yardımcılar ──

        private record Substitution(bool IsSuccess, string? Json, string? Code, string? Detail);

        private static async Task<Substitution> SubstituteEmbeddingsAsync(
            string raw, IEmbeddingGenerator<string, Embedding<float>> generator, CancellationToken ct)
        {
            var texts = new List<string>();
            foreach (Match m in PlaceholderRegex.Matches(raw))
                texts.Add(m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\"));

            if (texts.Count == 0)
            {
                // kalıntı {{embed (bozuk sözdizimi) yoksa ham gövde aynen döner.
                return MarkerLeftoverRegex.IsMatch(raw)
                    ? Bad("bozuk {{EMBED:\"metin\"}} yer-tutucusu")
                    : new Substitution(true, raw, null, null);
            }

            if (texts.Any(string.IsNullOrWhiteSpace))
                return Bad("boş {{EMBED}} metni");

            float[][] vectors;
            try
            {
                var embeddings = await generator.GenerateAsync(texts, cancellationToken: ct);
                vectors = embeddings.Select(e => e.Vector.ToArray()).ToArray();
            }
            catch (Exception ex)
            {
                return new Substitution(false, null,
                    StorefrontResourceConstants.STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE, FirstLine(ex.Message));
            }

            var i = 0;
            var substituted = PlaceholderRegex.Replace(raw, _ => ToJsonArray(vectors[i++]));

            if (MarkerLeftoverRegex.IsMatch(substituted))
                return Bad("bozuk {{EMBED:\"metin\"}} yer-tutucusu");

            return new Substitution(true, substituted, null, null);

            static Substitution Bad(string detail) =>
                new(false, null, StorefrontResourceConstants.STOREFRONT_SEARCH_BAD_QUERY, detail);
        }

        private static string ToJsonArray(float[] vector)
        {
            var parts = new string[vector.Length];
            for (var i = 0; i < vector.Length; i++)
                parts[i] = vector[i].ToString(CultureInfo.InvariantCulture);
            return $"[{string.Join(',', parts)}]";
        }

        private static QueryStorefrontResponse ParseHits(string responseBody, int? requestedSize)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var hits = doc.RootElement.GetProperty("hits");

            var rows = new List<Dictionary<string, object?>>();
            foreach (var hit in hits.GetProperty("hits").EnumerateArray())
            {
                if (!hit.TryGetProperty("_source", out var source))
                    continue;
                var row = new Dictionary<string, object?>();
                foreach (var prop in source.EnumerateObject())
                    row[prop.Name] = ToClr(prop.Value);
                rows.Add(row);
            }

            long total = hits.TryGetProperty("total", out var t) && t.TryGetProperty("value", out var v)
                ? v.GetInt64()
                : rows.Count;

            // Truncated: LLM tavanı aştı (clamp'lendi) VEYA toplam isabet dönenden fazla.
            var truncated = (requestedSize is int rs && rs > MaxRows) || total > rows.Count;

            return new QueryStorefrontResponse
            {
                Ok = true,
                Rows = rows,
                RowCount = rows.Count,
                Truncated = truncated
            };
        }

        private static object? ToClr(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => e.EnumerateArray().Select(ToClr).ToList(),
            JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => ToClr(p.Value)),
            _ => e.ToString()
        };

        private static FeatureObjectResultModel<QueryStorefrontResponse> Error(string code, string? property) =>
            FeatureObjectResultModel<QueryStorefrontResponse>.Error(new MessageItem { Code = code, Property = property });

        private static async Task WriteLog(IDocumentStore store, AgentQueryLog log, CancellationToken ct)
        {
            await using var session = store.LightweightSession();
            session.Store(log);
            await session.SaveChangesAsync(ct);
        }

        private static string FirstLine(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var newline = text.IndexOf('\n');
            return newline < 0 ? text : text[..newline];
        }
    }
}

// MCP tool ince sarmalayıcıdır; yalnız Features/Agents slice'ını çağırır (005). query_storefront =
// müşteri yüzeyinin TEK arama ucu (FR-010). Playbook (070 kanonik ev) bu Description'dadır — dış
// agent'lar ES DSL'i buradan öğrenir. Şema-drift guard (check-agent-query-schema.sh) bu dosyayı
// hedefler; ES alan adları düz metin kalmalı. (McpToolDescriptions.cs'teki bilinçli istisna: devasa
// const-composed description kendi dosyasında yaşar.)
[McpServerToolType]
public static class QueryStorefrontMcpTool
{
    private const string SchemaBlock =
        "Kitap mağazası vitrininde SERBEST Elasticsearch Query DSL (JSON) sorgusu çalıştırır. " +
        "\"Kitap ara\", \"şu temada kitap öner\", \"fiyatı şundan az kitaplar\" gibi serbest vitrin sorguları için. " +
        "Sabit index'e (yalnız SATILABILIR kitaplar) koşar; sen yalnız request body JSON'ı yazarsın " +
        "(index/_source/size sunucu yönetir). ALANLAR: " +
        "product_id (keyword; sepete-ekle/benzerlik anahtarı, kullanıcıya ham gösterme), " +
        "name (text tr + name.keyword exact/sort), description (text tr), authors (text tr; yazar adları), " +
        "publisher (text tr + .keyword), category (text tr + .keyword; İngilizce taksonomi), " +
        "price (float, TL liste fiyatı), effective_price (float, ödenecek: indirim aktifse indirimli), " +
        "stock (integer; alan yoksa bilinmiyor), rating_average (float; alan yoksa hiç puan yok), " +
        "rating_count (integer), specs (nested {attribute, option} keyword; özellik/varyant), " +
        "family_code (keyword; varyant ailesi, yoksa ailesiz), image_url (keyword; kapak), " +
        "added_at (date; YAKLAŞIK ekleniş), discount_pct (integer; aktif indirim yüzdesi, yoksa indirim yok), " +
        "discount_ends_at (date; aktif indirim bitişi), embedding (dense_vector; YANITA DÖNMEZ, yalnız knn). " +
        "KURALLAR: sonuç 50 satıra sınırlanır (size aşarsan truncated=true); embedding yanıta gelmez. ";

    private const string PatternsBlock =
        "DSL KALIPLARI: " +
        "(1) KATEGORİ İNGİLİZCE: kategori/tür adları İngilizcedir — kullanıcı Türkçe söylerse İngilizce " +
        "karşılığıyla ara ('kurgu/roman' → fiction, 'fantastik' → fantasy, 'bilim kurgu' → science fiction). " +
        "(2) Metin/ad: {\"query\":{\"match\":{\"name\":{\"query\":\"dune\",\"fuzziness\":\"AUTO\"}}}} — " +
        "yazım hatası toleransı için fuzziness:\"AUTO\" (FR-002). Çok alan: multi_match (name/authors/description). " +
        "(3) Filtre: range (price/effective_price/rating_average/stock), term (keyword alanlar). " +
        "(4) Özellik/varyant: nested sorgu {\"nested\":{\"path\":\"specs\",\"query\":{\"bool\":{\"must\":[" +
        "{\"term\":{\"specs.attribute\":\"Cilt\"}},{\"term\":{\"specs.option\":\"Ciltli\"}}]}}}}. " +
        "(5) BİRLEŞİM: tek sorguda bool (must/should/filter) ile metin + filtre + semantik. " +
        "(6) GENİŞ LİSTE: size ver (≤50); truncated=true dönerse 'hepsini gösterdim' DEME, kırpıldığını söyle. " +
        "(7) KAPAK: image_url zaten döner; kullanıcıya tıklanabilir link (markdown [Kapak](url)) olarak sun. ";

    private const string SemanticBlock =
        "TEMALI/ANLAMSAL arama (kNN): bulanık tema/ruh hali ifadesini {{EMBED:\"tema metni\"}} " +
        "yer-tutucusuyla yaz — sunucu vektöre çevirip knn.query_vector'a basar (ham vektör YAZMA). " +
        "Kalıp: {\"knn\":{\"field\":\"embedding\",\"query_vector\":{{EMBED:\"kış temalı sürükleyici bilim kurgu\"}}," +
        "\"k\":10,\"num_candidates\":50}}. Yapısal kısıt (fiyat/stok/kategori) AYNI sorguda bool.filter'a " +
        "eklenir (knn + filter hibrit). BENZERLİK ('buna benzer'): önce hedef ürünü bul (product_id), sonra " +
        "onun temasını {{EMBED}} ile ifade et YA DA hedefin konusunu metin olarak {{EMBED}}'e yaz. Alakasız " +
        "eşik-altı komşuyu 'benzer' diye sunma. ";

    private const string HonestyBlock =
        "DÜZELTME: hata dönerse (messages[].code + property ipucu) sorguyu düzeltip EN FAZLA 2 kez yeniden " +
        "dene; yine olmazsa bu sorunun şu an yanıtlanamadığını söyle — teknik ayrıntı dökme. " +
        "DÜRÜST VERİ SINIRI: satış adedi/bestseller verisi vitrinde YOK — 'en çok satan' sorulursa bu verinin " +
        "tutulmadığını dürüstçe söyle, asla uydurma. added_at YAKLAŞIKTIR. " +
        "GROUNDING: yanıtı YALNIZ dönen satırlardan kur. Boş sonuç = dürüst 'bulunamadı' (hata değildir); " +
        "asla satır/alan/değer uydurma, alakasız öneri sunma. product_id'yi kullanıcıya ham gösterme — " +
        "ad/fiyat/kapakla sun, id'yi yalnız diğer tool çağrılarında anahtar olarak kullan.";

    [McpServerTool(Name = Shared.StorefrontTools.QueryStorefront)]
    [Description(SchemaBlock + PatternsBlock + SemanticBlock + HonestyBlock)]
    public static Task<FeatureObjectResultModel<QueryStorefront.QueryStorefrontResponse>> QueryStorefrontAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Elasticsearch Query DSL request body (JSON); anlamsal metin {{EMBED:\"...\"}} ile knn.query_vector'a")]
        string query)
        => bus.InvokeAsync<FeatureObjectResultModel<QueryStorefront.QueryStorefrontResponse>>(
            new QueryStorefront.QueryStorefrontQuery(query), ct);
}

// NOT: keşif envanteri tool'ları (list_categories/authors/publishers) Catalog'dadır
// (envanter otoritesi = Catalog; Storefront = tek sorgu kapısı yüzeyi).