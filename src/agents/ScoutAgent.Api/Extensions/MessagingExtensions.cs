namespace ScoutAgent.Api.Extensions;

// ScoutAgent mesajlaşma: Wolverine süreç-içi bus (IMessageBus) — command/query handler keşfi.
// RabbitMQ YOK: ScoutAgent integration event yaymaz/tüketmez (başka BC'lerle tek kanalı MCP).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddScoutMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
        });

        return builder;
    }
}
