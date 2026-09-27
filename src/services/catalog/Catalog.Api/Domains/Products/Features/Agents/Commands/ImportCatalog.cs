using Catalog.Api.Import;
using Catalog.Api.Options;

namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// 083 US1/FR-001: Excel katalog import başlatır — token-yetkili yükleme ekranına SURELI + TEK KULLANIMLIK
// link üretir (3.7MB xlsx MCP arg'ına sığmaz; store'da web-login yüzeyi yok). Link tabanı config'ten
// (ImportOptions.PublicBaseUrl) — HttpContext base KULLANILMAZ (platform MCP fasad proxy, Aspire iç adresi;
// 078 emsali). Scope link ÜRETİMİNDE uygulanır (İLKE V v1.11.1 capability-link istisnası).
public static class ImportCatalog
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record ImportCatalogCommand(Guid UserId);

    public class ImportCatalogResponse
    {
        public string Url { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class ImportCatalogCommandHandler
    {
        public Task<FeatureObjectResultModel<ImportCatalogResponse>> Handle(
            ImportCatalogCommand cmd,
            IDocumentSession session,
            ImportOptions options,
            CancellationToken ct)
        {
            var created = ImportSession.Create(cmd.UserId, options.LinkLifetime);
            if (!created.IsSuccess)
                return Task.FromResult(FeatureObjectResultModel<ImportCatalogResponse>.Error(created.Messages));

            var importSession = created.Data!;
            session.Store(importSession);

            return Task.FromResult(FeatureObjectResultModel<ImportCatalogResponse>.Ok(new ImportCatalogResponse
            {
                Url = $"{options.PublicBaseUrl.TrimEnd('/')}/catalog-import/{importSession.Token}",
                ExpiresAt = importSession.ExpiresAt,
                Message = "Linki tarayicida acin; xlsx dosyasini ekrandan yukleyin. " +
                          "Link tek kullanimlik ve surelidir. Satirlar arka planda TASLAK urune donusur."
            }));
        }
    }
}

[McpServerToolType]
public static class ImportCatalogMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.ImportCatalog)]
    [Description(
        "YONETIM/YAZMA: Excel katalog import baslatir. DOSYA ISTEMEZ: kullanicidan xlsx dosyasini ISTEME, " +
        "onay bekleme, soru sorma — cagrildiginda HEMEN suurli + tek kullanimlik yukleme ekrani linki uret " +
        "ve yaniti dogrudan ver (dosya tarayicidaki ekrandan yuklenir, sohbete girmez). Yanit {url, " +
        "expiresAt, message}; sohbete yalniz linki dusur. Yuklenen satirlar arka planda TASLAK urune " +
        "donusur (yayin ayri: admin_publish_imported). Ilerleme/sonuc icin admin_get_import_status cagir.")]
    public static Task<FeatureObjectResultModel<ImportCatalog.ImportCatalogResponse>> ImportCatalogAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<ImportCatalog.ImportCatalogResponse>>(
            new ImportCatalog.ImportCatalogCommand(userId), ct);
    }
}
