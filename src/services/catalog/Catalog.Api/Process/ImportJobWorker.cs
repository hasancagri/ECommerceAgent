namespace Catalog.Api.Process;

// 004 US1 (iki-faz, DURUM-GÜDÜMLÜ): Excel import'un dayanıklı süreci (Process/ — BC'nin KENDİ işi).
// Mesaj/kuyruk YOK (kullanıcı kararı: handler'lar PublishAsync tetiği atmaz) — worker birkaç
// saniyede bir ImportJob tablosunu yoklar: Queued görür → dry-run koşar (tüm satırlar mapper'dan
// geçer, HİÇ yazılmaz; sebepli rapor birikir, iş AwaitingConfirmation'da onay bekler) · Confirmed
// görür → gerçek yazımı koşar: sayfa = yayınevi (R1); satır → ExcelRowMapper → INSERT-ONLY (ISBN
// varsa skip, FR-002); yazar/yayınevi/kategori get-or-create (batch-içi tekilleştirme — leaf==mid
// çift-Store tuzağı); her ürün doğumunda ProductAdded(CoverSourceUrl) yayılır (kapak zinciri R5).
// Satır hatası işi durdurmaz; sayaçlar ~BatchSize'da bir ImportJob'a yazılır. Üst-seviye hata →
// Job.Fail (retry YOK — tekrar yükleme ISBN-skip'le güvenli). Host kapanışında yarım kalan iş
// sonraki açılışta Fail'e çekilir (kurtarma turu). Yüklenen dosya SİLİNMEZ (kullanıcı kararı) —
// iş kaydının diskteki izi. Kayıt: Program.cs AddHostedService (Wolverine keşfiyle İLGİSİZ).
public sealed class ImportJobWorker(
    IDocumentStore store, IServiceScopeFactory scopes, ILogger<ImportJobWorker> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private const string FallbackCategoryName = "Genel";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            // stoppingToken iptali = host kapanıyor; iş Fail'lenmez (açılış kurtarması halleder).
            // Başka TaskCanceled (ör. HttpClient timeout) buraya girmez — tuzak notu: catch ayrımı şart.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import worker turu başarısız");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Açılış kurtarması: önceki süreç Validating/Running ortasında öldüyse iş sonsuza dek askıda
    // kalırdı (worker yalnız Queued/Confirmed yoklar). Fail'e çek — tekrar yükleme ISBN-skip'le güvenli.
    private async Task RecoverInterruptedJobsAsync(CancellationToken ct)
    {
        await using var session = store.LightweightSession();
        var stuck = await session.Query<ImportJob>()
            .Where(j => j.Status == ImportJobStatus.Validating || j.Status == ImportJobStatus.Running)
            .ToListAsync(ct);
        if (stuck.Count == 0)
            return;

        foreach (var job in stuck)
        {
            job.Fail("Servis yeniden başlatıldı; iş yarım kaldı. Dosyayı yeniden yükleyin.");
            session.Store(job);
            logger.LogWarning("Yarım kalan import işi Failed'e çekildi: {JobId}", job.Id);
        }
        await session.SaveChangesAsync(ct);
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Tek-aktif-iş kuralı (FR-008) handler'da; burada her turda en fazla bir iş ele alınır.
        var queuedId = await FindSingleAsync(ImportJobStatus.Queued, ct);
        if (queuedId is { } dryRunJob)
            await RunDryRunAsync(dryRunJob, ct);

        var confirmedId = await FindSingleAsync(ImportJobStatus.Confirmed, ct);
        if (confirmedId is { } importJob)
            await RunImportAsync(importJob, ct);
    }

    private async Task<Guid?> FindSingleAsync(ImportJobStatus status, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var job = await session.Query<ImportJob>().Where(j => j.Status == status).FirstOrDefaultAsync(ct);
        return job?.Id;
    }

    private async Task RunDryRunAsync(Guid jobId, CancellationToken ct)
    {
        var job = await TransitionAsync(jobId, j => j.StartValidation(), ct);
        if (job is null)
            return;

        try
        {
            await ProcessWorkbookAsync(job.Id, job.TempFilePath, dryRun: true, ct);
            await TransitionAsync(job.Id, j => j.CompleteValidation(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // host kapanışı — Fail'leme, açılış kurtarması devralır
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import dry-run başarısız: {JobId}", job.Id);
            await TransitionAsync(job.Id, j => j.Fail(ex.Message), ct);
        }
    }

    private async Task RunImportAsync(Guid jobId, CancellationToken ct)
    {
        var job = await TransitionAsync(jobId, j => j.Start(), ct);
        if (job is null)
            return;

        try
        {
            await ProcessWorkbookAsync(job.Id, job.TempFilePath, dryRun: false, ct);
            await TransitionAsync(job.Id, j => j.Complete(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import işi başarısız: {JobId}", job.Id);
            await TransitionAsync(job.Id, j => j.Fail(ex.Message), ct);
        }
    }

    // Durum geçişini yükle-uygula-kaydet olarak tek yerde yapar. Geçersiz geçiş null döner —
    // yeniden işleme yok (FR-008 güvenliği), yalnız log.
    private async Task<ImportJob?> TransitionAsync(Guid jobId, Func<ImportJob, ResultDomain> transition, CancellationToken ct)
    {
        await using var session = store.LightweightSession();
        var job = await session.LoadAsync<ImportJob>(jobId, ct);
        if (job is null)
        {
            logger.LogWarning("Import işi bulunamadı: {JobId}", jobId);
            return null;
        }

        var result = transition(job);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Import işi geçersiz durumda, atlandı: {JobId} ({Status})", job.Id, job.Status);
            return null;
        }

        session.Store(job);
        await session.SaveChangesAsync(ct);
        return job;
    }

    private async Task ProcessWorkbookAsync(Guid jobId, string filePath, bool dryRun, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(filePath);
        var lookups = await PreloadLookupsAsync(ct);

        foreach (var sheet in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessSheetAsync(jobId, sheet, lookups, dryRun, ct);
        }
    }

    // Mevcut dünya tek seferde belleğe: 23.600 satır × tek tek sorgu yerine sözlük lookup.
    private async Task<Lookups> PreloadLookupsAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        return new Lookups(
            (await session.Query<Product>().Select(p => p.Gtin).ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            (await session.Query<Author>().ToListAsync(ct)).ToDictionary(a => a.NormalizedName, a => a),
            (await session.Query<Publisher>().ToListAsync(ct)).ToDictionary(p => p.NormalizedName, p => p),
            (await session.Query<Category>().ToListAsync(ct)).ToDictionary(c => c.NormalizedName, c => c));
    }

    private async Task ProcessSheetAsync(Guid jobId, IXLWorksheet sheet, Lookups lookups, bool dryRun, CancellationToken ct)
    {
        var headers = sheet.Row(1).CellsUsed()
            .ToDictionary(c => c.Address.ColumnNumber, c => c.GetString().Trim());

        var batch = new Batch();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var cells = headers.ToDictionary(
                h => h.Value,
                h => (object?)(row.Cell(h.Key).IsEmpty() ? null : row.Cell(h.Key).Value.ToString()));

            ProcessRow(sheet.Name, row.RowNumber(), cells, lookups, batch);

            if (batch.Processed >= BatchSize)
                await FlushAsync(jobId, batch, dryRun, ct);
        }
        await FlushAsync(jobId, batch, dryRun, ct);
    }

    private void ProcessRow(
        string sheetName, int rowNumber, IReadOnlyDictionary<string, object?> cells,
        Lookups lookups, Batch batch)
    {
        batch.Processed++;

        var mapped = ExcelRowMapper.Map(sheetName, rowNumber, cells);
        if (mapped.Error is not null)
        {
            batch.Errors.Add(new ImportError(sheetName, rowNumber, mapped.Error));
            return;
        }

        var importRow = mapped.Row!;
        // INSERT-ONLY (FR-002): katalogda ya da bu dosyanın önceki satırında varsa DOKUNMA.
        if (!lookups.ExistingGtins.Add(importRow.Isbn))
        {
            batch.Skipped++;
            return;
        }

        var price = importRow.Price is { } amount ? Money.Create(amount) : null;
        var created = Product.Create(importRow.Name, importRow.Isbn, price, importRow.Description);
        if (!created.IsSuccess)
        {
            batch.Errors.Add(new ImportError(sheetName, rowNumber,
                string.Join("; ", created.Messages.Select(m => m.Code))));
            return;
        }
        var product = created.Data!;

        // Yayınevi (sayfa adından) + yazarlar get-or-create — batch-içi tekilleştirme sözlüklerle
        // (Marten leaf==mid dersi: aynı normalize ada İKİNCİ Store atılmaz).
        var publisher = GetOrCreate(lookups.Publishers, importRow.PublisherName,
            name => Publisher.Create(name).Data!, batch);
        product.SetPublisher(publisher.Id);

        var authorIds = new List<Guid>();
        foreach (var authorName in importRow.AuthorNames)
        {
            var author = GetOrCreate(lookups.Authors, authorName, name => Author.Create(name).Data!, batch);
            author.SetBio(importRow.AuthorBio); // ilk-dolu kazanır (Author guard'ı)
            batch.Stores.Add(author);
            authorIds.Add(author.Id);
        }
        product.SetAuthors(authorIds);

        // Kategori: leaf (+ opsiyonel üst) get-or-create; boş → "Genel" fallback (FLOW kuralı).
        var leaf = ResolveCategory(importRow.ParentCategoryName, importRow.LeafCategoryName, lookups.Categories, batch);
        product.SetCategory(leaf.Id);

        // İlgili kategoriler: YALNIZ çözülebilen (FLOW: "çözülemeyen atlanır") — get-or-create YOK.
        var related = importRow.RelatedCategoryLeafNames
            .Select(NameNormalization.Normalize)
            .Where(lookups.Categories.ContainsKey)
            .Select(n => lookups.Categories[n].Id)
            .ToList();
        product.SetRelatedCategories(related);

        product.SetTranslators(importRow.Translators);
        product.SetBookMeta(importRow.PageCount, importRow.PublishedOn, importRow.Language);
        product.SetBinding(importRow.Binding);
        product.SetPopularity(importRow.Rating, importRow.RatingCount, importRow.PurchaseCount,
            importRow.ToReadCount, importRow.ReadingCount, importRow.ReadCount, importRow.FavoriteCount);

        batch.Stores.Add(product);
        batch.Added++;
        batch.Events.Add(new IntegrationEvents.ProductAdded(product.Gtin, product.Id, importRow.CoverUrl));
    }

    private static T GetOrCreate<T>(Dictionary<string, T> cache, string name,
        Func<string, T> factory, Batch batch) where T : AggregateRoot
    {
        var normalized = NameNormalization.Normalize(name);
        if (cache.TryGetValue(normalized, out var existing))
            return existing;

        var created = factory(name);
        cache[normalized] = created;
        batch.Stores.Add(created);
        return created;
    }

    private static Category ResolveCategory(string? parentName, string? leafName,
        Dictionary<string, Category> categories, Batch batch)
    {
        var effectiveLeaf = string.IsNullOrWhiteSpace(leafName) ? FallbackCategoryName : leafName;

        // leaf == üst (aynı ad) → tek düğüm (leaf==mid gotcha'sının ad-düzeyi hali).
        if (!string.IsNullOrWhiteSpace(parentName)
            && !NameNormalization.Normalize(parentName).Equals(NameNormalization.Normalize(effectiveLeaf)))
        {
            var parent = GetOrCreate(categories, parentName,
                name => Category.Create(name).Data!, batch);
            return GetOrCreate(categories, effectiveLeaf,
                name => Category.Create(name, parent.Id).Data!, batch);
        }

        return GetOrCreate(categories, effectiveLeaf, name => Category.Create(name).Data!, batch);
    }

    private async Task FlushAsync(Guid jobId, Batch batch, bool dryRun, CancellationToken ct)
    {
        if (batch.Processed == 0)
            return;

        await using var session = store.LightweightSession();
        // Dry-run yalnız raporu yazar — ürün/yazar/kategori diske İNMEZ, event ÇIKMAZ.
        if (!dryRun)
            session.StoreObjects(batch.Stores.Distinct().ToList());

        var job = (await session.LoadAsync<ImportJob>(jobId, ct))!;
        job.RecordProgress(batch.Processed, batch.Added, batch.Skipped, batch.Errors);
        session.Store(job);

        await session.SaveChangesAsync(ct);

        // Yayın commit SONRASI (kapak zinciri at-least-once; çökme arasında kalan kapaklar
        // tekrar yüklemede R2-mevcut yolundan kendini onarır). IMessageBus scoped — worker
        // singleton olduğundan scope burada açılır. Bu BC-arası EVENT yayınıdır (ProductAdded →
        // File), komut tetiği değil — "handler'da PublishAsync olmasın" kuralının dışında.
        if (!dryRun && batch.Events.Count > 0)
        {
            using var scope = scopes.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            foreach (var evt in batch.Events)
                await bus.PublishAsync(evt);
        }

        batch.Reset();
    }

    // Ön-yüklenen mevcut dünya: satır işleme sırasında DB'ye inmeden kimlik/tekilleştirme sözlükleri.
    private sealed record Lookups(
        HashSet<string> ExistingGtins,
        Dictionary<string, Author> Authors,
        Dictionary<string, Publisher> Publishers,
        Dictionary<string, Category> Categories);

    private sealed class Batch
    {
        public int Processed;
        public int Added;
        public int Skipped;
        public List<ImportError> Errors { get; } = new();
        public List<object> Stores { get; } = new();
        public List<IntegrationEvents.ProductAdded> Events { get; } = new();

        public void Reset()
        {
            Processed = 0;
            Added = 0;
            Skipped = 0;
            Errors.Clear();
            Stores.Clear();
            Events.Clear();
        }
    }
}
