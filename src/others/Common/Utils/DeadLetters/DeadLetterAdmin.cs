using System.ComponentModel;
using Common.Utils.Authorization;
using ModelContextProtocol.Server;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;

namespace Common.Utils.DeadLetters;

// 088: ölü-mesaj operatör yüzeyi (çapraz-kesen altyapı, domain değil → Common; bkz. "infra'yı paylaş,
// domain'i tekrarla"). Wolverine'in IDeadLetters API'sini sarmalar — her BC KENDİ durable store'unu
// (<şema>.wolverine_dead_letters) sorgular (BC izole; merkezi tablo yok). Ham mesaj gövdesi
// (DeadLetterEnvelope.Message) yüzeye/log'a KASITLI dönmez — yalnız metadata + hata (FR-004). Her servis
// bu tool'u .WithTools<DeadLetterAdminMcpTools>() ile kaydeder + handler'ları
// opts.Discovery.IncludeType(typeof(DeadLetterAdminHandlers)) ile dahil eder.
public static class DeadLetterAdmin
{
    // Yüzeye dönen güvenli görünüm — Wolverine DeadLetterEnvelope'un BC-agnostik izdüşümü (ham body hariç).
    public class DeadLetterItem
    {
        public Guid Id { get; set; }
        public string? MessageType { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset SentAt { get; set; }
    }

    [RequiredScope(AuthorizationScopes.OpsDeadletter)]
    public record ListDeadLettersCommand(string? MessageType, int Limit);

    [RequiredScope(AuthorizationScopes.OpsDeadletter)]
    public record GetDeadLetterCommand(Guid Id);

    [RequiredScope(AuthorizationScopes.OpsDeadletter)]
    public record ReplayDeadLetterCommand(Guid Id);

    [RequiredScope(AuthorizationScopes.OpsDeadletter)]
    public record DiscardDeadLetterCommand(Guid Id);

    internal static DeadLetterItem Map(DeadLetterEnvelope e) => new()
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
    public async Task<FeatureListResultModel<DeadLetterAdmin.DeadLetterItem>> Handle(
        DeadLetterAdmin.ListDeadLettersCommand cmd, IMessageStore store, CancellationToken ct)
    {
        var results = await store.DeadLetters.QueryAsync(new DeadLetterEnvelopeQuery(), ct);
        var items = results.Envelopes
            .Where(e => cmd.MessageType is null || e.MessageType == cmd.MessageType)
            .OrderByDescending(e => e.SentAt)
            .Take(cmd.Limit <= 0 ? 50 : cmd.Limit)
            .Select(DeadLetterAdmin.Map)
            .ToList();
        return FeatureListResultModel<DeadLetterAdmin.DeadLetterItem>.Ok(items);
    }

    public async Task<FeatureObjectResultModel<DeadLetterAdmin.DeadLetterItem>> Handle(
        DeadLetterAdmin.GetDeadLetterCommand cmd, IMessageStore store, CancellationToken ct)
    {
        var envelope = await store.DeadLetters.DeadLetterEnvelopeByIdAsync(cmd.Id);
        return envelope is null
            ? FeatureObjectResultModel<DeadLetterAdmin.DeadLetterItem>.NotFound()
            : FeatureObjectResultModel<DeadLetterAdmin.DeadLetterItem>.Ok(DeadLetterAdmin.Map(envelope));
    }

    public async Task<FeatureResultModel> Handle(
        DeadLetterAdmin.ReplayDeadLetterCommand cmd, IMessageStore store, CancellationToken ct)
    {
        await store.DeadLetters.MarkDeadLetterEnvelopesAsReplayableAsync(new[] { cmd.Id });
        return FeatureResultModel.Ok();
    }

    public async Task<FeatureResultModel> Handle(
        DeadLetterAdmin.DiscardDeadLetterCommand cmd, IMessageStore store, CancellationToken ct)
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
    public static Task<FeatureListResultModel<DeadLetterAdmin.DeadLetterItem>> ListAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yalnız bu mesaj tipine göre süz (opsiyonel)")] string? messageType = null,
        [Description("En çok kaç kayıt dönsün (varsayılan 50)")] int limit = 50)
        => bus.InvokeAsync<FeatureListResultModel<DeadLetterAdmin.DeadLetterItem>>(
            new DeadLetterAdmin.ListDeadLettersCommand(messageType, limit), ct);

    [McpServerTool(Name = Shared.DeadLetterAdminTools.GetDeadLetter)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.GetDeadLetter)]
    public static Task<FeatureObjectResultModel<DeadLetterAdmin.DeadLetterItem>> GetAsync(
        [Description("Ölü mesaj kimliği (admin_list_dead_letters'tan)")] Guid id,
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<DeadLetterAdmin.DeadLetterItem>>(
            new DeadLetterAdmin.GetDeadLetterCommand(id), ct);

    [McpServerTool(Name = Shared.DeadLetterAdminTools.ReplayDeadLetter)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.ReplayDeadLetter)]
    public static Task<FeatureResultModel> ReplayAsync(
        [Description("Ölü mesaj kimliği (admin_list_dead_letters'tan)")] Guid id,
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureResultModel>(new DeadLetterAdmin.ReplayDeadLetterCommand(id), ct);

    [McpServerTool(Name = Shared.DeadLetterAdminTools.DiscardDeadLetter)]
    [Description(Shared.McpToolDescriptions.DeadLetterAdminTools.DiscardDeadLetter)]
    public static Task<FeatureResultModel> DiscardAsync(
        [Description("Ölü mesaj kimliği (admin_list_dead_letters'tan)")] Guid id,
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureResultModel>(new DeadLetterAdmin.DiscardDeadLetterCommand(id), ct);
}
