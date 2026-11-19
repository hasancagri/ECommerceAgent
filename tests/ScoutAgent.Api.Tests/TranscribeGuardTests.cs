using ScoutAgent.Api.Constants;
using ScoutAgent.Api.Domains.Conversations.Features.Queries;

namespace ScoutAgent.Api.Tests;

// 020-voice: ses boyut/tip guard'ı saf birim doğrulaması (İlke VI test-first).
public class TranscribeGuardTests
{
    private const long Max = 10 * 1024 * 1024;

    [Fact]
    public void Validate_EmptyAudio_ReturnsError()
    {
        var result = TranscribeGuard.Validate(0, "audio/webm;codecs=opus", Max);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == ScoutResourceConstants.TRANSCRIBE_AUDIO_EMPTY);
    }

    [Fact]
    public void Validate_TooLarge_ReturnsError()
    {
        var result = TranscribeGuard.Validate(Max + 1, "audio/webm", Max);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == ScoutResourceConstants.TRANSCRIBE_AUDIO_TOO_LARGE);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_UnsupportedType_ReturnsError(string? contentType)
    {
        var result = TranscribeGuard.Validate(1024, contentType, Max);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == ScoutResourceConstants.TRANSCRIBE_AUDIO_UNSUPPORTED);
    }

    [Theory]
    [InlineData("audio/webm;codecs=opus")]
    [InlineData("audio/mp4")]
    [InlineData("audio/mpeg")]
    [InlineData("AUDIO/OGG")]
    public void Validate_ValidAudio_ReturnsSuccess(string contentType)
    {
        var result = TranscribeGuard.Validate(1024, contentType, Max);

        result.IsSuccess.ShouldBeTrue();
    }
}
