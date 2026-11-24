using System.ClientModel;
using Common.Dependencies;
using Microsoft.Extensions.AI;
using OpenAI;
using Personalization.Api.Options;

namespace Personalization.Api.Search;

// 028: taste-vektör için OpenAI embedding üreticisi (Catalog 025 ChatClientFactory/EmbeddingClientFactory
// deseni). Singleton; batch `GenerateAsync(IEnumerable<string>)` sorgu metinlerini embed eder. Model
// Catalog'la AYNI (aynı uzay). API key İlke VIII: sunucuda, loglanmaz.
public sealed class EmbeddingClientFactory(PersonalizationEmbeddingOptions options) : ISingletonDependency
{
    public IEmbeddingGenerator<string, Embedding<float>> Generator { get; } =
        new OpenAIClient(new ApiKeyCredential(options.ApiKey))
            .GetEmbeddingClient(options.Model)
            .AsIEmbeddingGenerator();
}
