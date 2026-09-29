using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using Reviews.Moderation.Options;

namespace Reviews.Moderation;

// in-process Singleton ChatClientAgent (EnrichmentAgent emsali) — MCP'siz,
// Temperature=0, structured JSON. Yalniz KARAR verir; gizlemeyi Reviews'te Review.ApplyModeration uygular.
// PII gonderilmez: prompt'a yalniz yorum metni + yildiz gider (kullanici adi/Id ASLA).
public sealed class ModerationAgent
{
    // Structured cikti sozlesmesi: kapali kategori enum'u (profanity/insult/personal_attack/none).
    public record ModerationOutput(bool Violation, string Category, string Reason);

    private const string Instructions =
        """
        Sen bir e-ticaret urun yorumu denetcisisin. Sana bir yorumun metni ve yildiz puani verilir.
        Yorumun kufur, hakaret veya kisisel saldiri icerip icermedigine karar ver.
        Kurallar:
        - violation=true YALNIZ su kategorilerde: profanity (kufur), insult (hakaret),
          personal_attack (kisisel saldiri). Temiz yorumda violation=false ve category="none".
        - Elestirel ama kufursuz olumsuz yorum IHLAL DEGILDIR ("urun berbat, tavsiye etmem" serbest).
        - Urune/markaya sert elestiri serbest; kisiye (satici, diger kullanici, calisan) hakaret ihlaldir.
        - reason: en fazla 200 karakter, kisa Turkce gerekce. Yuzeye cikmaz, yalniz iz.
        """;

    private readonly ChatClientAgent _agent;

    public ModerationAgent(ModerationOptions options)
    {
        IChatClient chatClient = new OpenAIClient(options.ApiKey)
            .GetChatClient(options.Model)
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(o => o.ModelId = options.Model)
            .Build();

        _agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "reviews-moderation",
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Temperature = 0, // karar yolunda varyans istenmez (EnrichmentAgent mirasi)
            },
        });
    }

    public async Task<ModerationOutput> ModerateAsync(string text, int rating, CancellationToken ct)
    {
        var prompt =
            $"""
             Yildiz puani: {rating}
             Yorum metni:
             {text}
             """;

        var response = await _agent.RunAsync<ModerationOutput>(prompt, cancellationToken: ct);
        return response.Result;
    }
}
