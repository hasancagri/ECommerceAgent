using ScoutAgent.Api.Options;

namespace ScoutAgent.Api.Agents;

// 019: agent-başına model çözümleme (saf — İlke VI test-first). Sub-agent kendi Key'iyle çağırır.
public static class ScoutModelResolver
{
    // BC anahtarı → model. Agents haritasında dolu değer varsa onu, yoksa genel varsayılanı (Model).
    public static string Resolve(ScoutOptions options, string key) =>
        options.Agents.TryGetValue(key, out var model) && !string.IsNullOrWhiteSpace(model)
            ? model : options.Model;

    // Router modeli — RouterModel atanmışsa o, yoksa genel varsayılan (ucuz/hızlı niyet-dağıtımı).
    public static string ResolveRouter(ScoutOptions options) =>
        string.IsNullOrWhiteSpace(options.RouterModel) ? options.Model : options.RouterModel;
}
