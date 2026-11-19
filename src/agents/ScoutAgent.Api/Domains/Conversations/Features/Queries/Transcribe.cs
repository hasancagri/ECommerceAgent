using OpenAI.Audio;
using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.Domains.Conversations.Features.Queries;

// 020-voice: ses → metin (STT). Kullanıcı-tetikli okuma-benzeri niyet → Features/Queries slice. Ses stream'i
// doğrudan OpenAI AudioClient'a geçer, DİSKE YAZILMAZ (İlke VIII — geçici, iz yok). Guard (boyut/tip) burada;
// başarısızsa Error → endpoint 400. OpenAI erişilemezse exception → endpoint 503 + kota iadesi (/messages deseni).
public static class Transcribe
{
    public record Query(Stream Audio, string FileName, string? ContentType, long Length, long MaxBytes);

    public class Response
    {
        public string Text { get; set; } = string.Empty;
    }

    public class TranscribeHandler(AudioClientFactory factory)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var guard = TranscribeGuard.Validate(query.Length, query.ContentType, query.MaxBytes);
            if (!guard.IsSuccess)
                return FeatureObjectResultModel<Response>.Error(guard.Messages);

            // Dil ipucu tr → Türkçe doğruluk. Metin dışı çıktı istemiyoruz (yalnız .Text).
            var options = new AudioTranscriptionOptions { Language = "tr" };
            var result = await factory.Client.TranscribeAudioAsync(query.Audio, query.FileName, options, ct);

            // Boş/sessiz ses → boş metin; istemci "bir şey duyamadım" gösterir (edge, 200 OK).
            return FeatureObjectResultModel<Response>.Ok(new Response { Text = result.Value.Text ?? string.Empty });
        }
    }
}
