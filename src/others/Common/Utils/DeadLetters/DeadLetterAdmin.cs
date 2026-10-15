namespace Common.Utils.DeadLetters;

// 088: ölü-mesaj operatör yüzeyi (çapraz-kesen altyapı, domain değil → Common; bkz. "infra'yı paylaş,
// domain'i tekrarla"). Wolverine'in IDeadLetters API'sini sarmalar — her BC KENDİ durable store'unu
// (<şema>.wolverine_dead_letters) sorgular (BC izole; merkezi tablo yok). Ham mesaj gövdesi
// (DeadLetterEnvelope.Message) yüzeye/log'a KASITLI dönmez — yalnız metadata + hata (FR-004). Her servis
// bu tool'u .WithTools<DeadLetterAdminMcpTools>() ile kaydeder + handler'ları
// opts.Discovery.IncludeType(typeof(DeadLetterAdminHandlers)) ile dahil eder.
// YETKİ: [RequiredScope] bu MESAJ tiplerinden okunur; ScopeAuthorizationMiddleware.Before mesaj
// Wolverine pipeline'ından (bus.InvokeAsync) geçerken scope'u denetler. Bu yüzden MCP tool asla
// store.DeadLetters'ı doğrudan çağırmaz — her zaman InvokeAsync(komut) ile Handle'a gider; aksi halde
// mesaj hiç oluşmaz, middleware tetiklenmez, scope kontrolü ATLANIR (sessiz yetki açığı). Handle
// dolaylaması = yetki kapısı; kaldırma.
[RequiredScope(AuthorizationScopes.OpsDeadletter)]
public record ListDeadLettersCommand(string? MessageType, int Limit);

[RequiredScope(AuthorizationScopes.OpsDeadletter)]
public record GetDeadLetterCommand(Guid Id);

[RequiredScope(AuthorizationScopes.OpsDeadletter)]
public record ReplayDeadLetterCommand(Guid Id);

[RequiredScope(AuthorizationScopes.OpsDeadletter)]
public record DiscardDeadLetterCommand(Guid Id);

// Yüzeye dönen güvenli görünüm — Wolverine DeadLetterEnvelope'un BC-agnostik izdüşümü (ham body hariç).
// Class (record değil): FeatureObjectResultModel<T> parametresiz ctor (T : new()) ister.
public class DeadLetterItem
{
    public Guid Id { get; set; }
    public string? MessageType { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? Source { get; set; }
    public DateTimeOffset SentAt { get; set; }

    public static DeadLetterItem From(DeadLetterEnvelope e) => new()
    {
        Id = e.Id,
        MessageType = e.MessageType,
        ExceptionType = e.ExceptionType,
        ExceptionMessage = e.ExceptionMessage,
        Source = e.Source,
        SentAt = e.SentAt,
    };
}

// Tek handler sınıfı (4 Handle) → servis başına tek IncludeType. Marten yazması yok ([Transactional]
// gerekmez): IDeadLetters doğrudan Wolverine message store'unu mutasyonlar, BC'nin kendi DB'sinde.
public class DeadLetterAdminHandlers
{
    public async Task<FeatureListResultModel<DeadLetterItem>> Handle(
        ListDeadLettersCommand cmd, IMessageStore store, CancellationToken ct)
    {
        var results = await store.DeadLetters.QueryAsync(new DeadLetterEnvelopeQuery(), ct);
        var items = results.Envelopes
            .Where(e => cmd.MessageType is null || e.MessageType == cmd.MessageType)
            .OrderByDescending(e => e.SentAt)
            .Take(cmd.Limit <= 0 ? 50 : cmd.Limit)
            .Select(DeadLetterItem.From)
            .ToList();
        return FeatureListResultModel<DeadLetterItem>.Ok(items);
    }

    public async Task<FeatureObjectResultModel<DeadLetterItem>> Handle(
        GetDeadLetterCommand cmd, IMessageStore store, CancellationToken ct)
    {
        var envelope = await store.DeadLetters.DeadLetterEnvelopeByIdAsync(cmd.Id);
        return envelope is null
            ? FeatureObjectResultModel<DeadLetterItem>.NotFound()
            : FeatureObjectResultModel<DeadLetterItem>.Ok(DeadLetterItem.From(envelope));
    }

    public async Task<FeatureResultModel> Handle(
        ReplayDeadLetterCommand cmd, IMessageStore store, CancellationToken ct)
    {
        await store.DeadLetters.ReplayAsync(
            new DeadLetterEnvelopeQuery { MessageIds = new[] { cmd.Id } }, ct);
        return FeatureResultModel.Ok();
    }

    public async Task<FeatureResultModel> Handle(
        DiscardDeadLetterCommand cmd, IMessageStore store, CancellationToken ct)
    {
        await store.DeadLetters.DiscardAsync(
            new DeadLetterEnvelopeQuery { MessageIds = new[] { cmd.Id } }, ct);
        return FeatureResultModel.Ok();
    }
}

// NOT static: .WithTools<T>() generic kaydı statik tip almaz (CS0718); metotlar statik kalır.
[McpServerToolType]
public class DeadLetterAdminMcpTools
{
    [McpServerTool(Name = Shared.DeadLetterAdminTools.ListDeadLetters)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.ListDeadLetters)]
    public static Task<FeatureListResultModel<DeadLetterItem>> ListAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yalnız bu mesaj tipine göre süz (opsiyonel)")] string? messageType = null,
        [Description("En çok kaç kayıt dönsün (varsayılan 50)")] int limit = 50)
        => bus.InvokeAsync<FeatureListResultModel<DeadLetterItem>>(
            new ListDeadLettersCommand(messageType, limit), ct);

    [McpServerTool(Name = Shared.DeadLetterAdminTools.GetDeadLetter)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.GetDeadLetter)]
    public static Task<FeatureObjectResultModel<DeadLetterItem>> GetAsync(
        [Description("Ölü mesaj kimliği (admin_list_dead_letters'tan)")] Guid id,
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<DeadLetterItem>>(
            new GetDeadLetterCommand(id), ct);

    [McpServerTool(Name = Shared.DeadLetterAdminTools.ReplayDeadLetter)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.ReplayDeadLetter)]
    public static Task<FeatureResultModel> ReplayAsync(
        [Description("Ölü mesaj kimliği (admin_list_dead_letters'tan)")] Guid id,
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureResultModel>(new ReplayDeadLetterCommand(id), ct);

    [McpServerTool(Name = Shared.DeadLetterAdminTools.DiscardDeadLetter)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.DiscardDeadLetter)]
    public static Task<FeatureResultModel> DiscardAsync(
        [Description("Ölü mesaj kimliği (admin_list_dead_letters'tan)")] Guid id,
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureResultModel>(new DiscardDeadLetterCommand(id), ct);
}
