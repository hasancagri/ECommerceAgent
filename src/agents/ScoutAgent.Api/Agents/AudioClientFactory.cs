using System.ClientModel;
using OpenAI;
using OpenAI.Audio;

namespace ScoutAgent.Api.Agents;

// 020-voice: OpenAI AudioClient (ses → metin) sağlayıcısı. ChatClientFactory deseni — key ScoutOptions'tan
// (aynı kaynak, İlke VIII: sunucuda kalır). Tek model (TranscribeModel) → tek client, cache'lenir. Singleton.
public sealed class AudioClientFactory(ScoutOptions options) : ISingletonDependency
{
    private readonly Lazy<AudioClient> _client = new(() =>
        new OpenAIClient(new ApiKeyCredential(options.ApiKey)).GetAudioClient(options.TranscribeModel));

    public AudioClient Client => _client.Value;
}
