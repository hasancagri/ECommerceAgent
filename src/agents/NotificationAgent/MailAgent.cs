namespace NotificationAgent;

// TEK mail agent'i (kullanici karari: compose+send ayri agent'lar birlestirildi — mail basina
// tek LLM cagrisi). Maili Turkce yazar VE Mail.Mcp'nin send_mail tool'unu LLM tool-secimiyle
// cagirir; imperatif CallToolAsync YOK (anayasa v1.8.1). Tool'lar her cagrida taze kesifle alinir.
// Her LLM/MCP/SMTP hatasi ayni kapiya cikar: NotificationException → retry → error queue (FR-008;
// eski "yedek sablon" yolu birlesmeyle kalkti — Send de LLM oldugundan degeri zaten dardi).
public sealed class MailAgent(
    NotificationOptions options,
    IHttpClientFactory httpClientFactory,
    ILogger<MailAgent> logger)
{
    private readonly ChatClientAgent _agent = new(
        new OpenAIClient(options.ApiKey)
            .GetChatClient(options.Model)
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(o => o.ModelId = options.Model)
            .Build(),
        new ChatClientAgentOptions
        {
            Name = "notification-mail",
            ChatOptions = new ChatOptions
            {
                Instructions = Prompts.MailInstructions,
            },
        });

    /// <summary>Tetik verisinden Turkce maili yazdirip send_mail ile gonderir; her hatada NotificationException firlatir.</summary>
    public async Task SendPriceAlarmMailAsync(
        IntegrationEvents.PriceAlarmTriggered evt, CancellationToken ct)
    {
        string text;
        try
        {
            var httpClient = httpClientFactory.CreateClient(MailMcp.ClientName);
            await using var mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions
                    {
                        Name = MailMcp.ClientName,
                        Endpoint = new Uri(MailMcp.Url),
                    },
                    httpClient,
                    ownsHttpClient: false),
                cancellationToken: ct);

            var tools = await mcpClient.ListToolsAsync(cancellationToken: ct);

            var prompt =
                $"""
                 Alici e-posta: {evt.Email}
                 Urun adi: {evt.ProductName}
                 Eski fiyat: {evt.OldPrice:0.00} TL
                 Yeni fiyat: {evt.NewPrice:0.00} TL
                 """;
            var response = await _agent.RunAsync(prompt,
                options: new ChatClientAgentRunOptions(new ChatOptions
                {
                    Tools = [.. tools],
                }),
                cancellationToken: ct);

            text = response.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Mail uretimi/gonderimi basarisiz (LLM/MCP/SMTP).");
            throw new NotificationException($"send-error: {Truncate(ex.Message)}", ex);
        }

        // Tool sonucu "sent:<id>" icermiyorsa gonderim kanitsizdir — basarisiz say (retry).
        if (!text.Contains(MailMcp.SentMarker))
            throw new NotificationException($"send-not-confirmed: {Truncate(text)}");
    }

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : value[..200];
}