using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Catalog.Api.Search;

// 025 Dilim-1: OpenAI embedding üreticisi (ScoutAgent ChatClientFactory deseni). Singleton (Scrutor);
// per-çağrı davranış yok → tek client. `GenerateAsync(IEnumerable<string>)` batch embed destekler.
// API key İlke VIII: yalnız sunucuda, loglanmaz.
public sealed class EmbeddingClientFactory(CatalogEmbeddingOptions options) : ISingletonDependency
{
    public IEmbeddingGenerator<string, Embedding<float>> Generator { get; } =
        new OpenAIClient(new ApiKeyCredential(options.ApiKey))
            .GetEmbeddingClient(options.Model)
            .AsIEmbeddingGenerator();
}
