namespace ScoutAgent.Api.Domains.Conversations.Features.Queries;

// 020-voice: ses dosyası boyut/tip guard'ı — saf birim (host'suz test edilebilir, İlke VI). Slice + endpoint
// buradan geçer; istemciye güvenme (≈60sn cap sunucuda boyutla da doğrulanır). content-type codecs param
// taşıyabilir (audio/webm;codecs=opus) → yalnız "audio/" öneki aranır.
public static class TranscribeGuard
{
    public static ResultDomain Validate(long length, string? contentType, long maxBytes)
    {
        if (length <= 0)
            return ResultDomain.Error(new MessageItem { Code = ScoutResourceConstants.TRANSCRIBE_AUDIO_EMPTY });
        if (length > maxBytes)
            return ResultDomain.Error(new MessageItem { Code = ScoutResourceConstants.TRANSCRIBE_AUDIO_TOO_LARGE });
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.TrimStart().StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return ResultDomain.Error(new MessageItem { Code = ScoutResourceConstants.TRANSCRIBE_AUDIO_UNSUPPORTED });
        return ResultDomain.Ok();
    }
}
