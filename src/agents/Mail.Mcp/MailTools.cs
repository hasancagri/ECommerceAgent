using System.ComponentModel;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ModelContextProtocol.Server;

namespace Mail.Mcp;

// repo'nun ilk standalone MCP server tool'u. Tek is: MailKit ile SMTP'ye (Mailpit) gonderim.
// TUM param'lar ZORUNLU (optional-param default tuzagina girilmez — memory: mcp-tool-optional-param-default).
// SMTP hatasi exception olarak yukselir → MCP error → Send agent'i basarisizligi worker'a tasir.
[McpServerToolType]
public static class MailTools
{
    [McpServerTool(Name = Shared.MailTools.SendMail)]
    [Description("Bir e-posta gonderir. Basarida 'sent:<messageId>' doner; hatada hata firlatir.")]
    public static async Task<string> SendMailAsync(
        [Description("Alici e-posta adresi")] string to,
        [Description("Konu (Turkce)")] string subject,
        [Description("HTML govde")] string bodyHtml,
        SmtpOptions options,
        CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = bodyHtml }.ToMessageBody();

        using var client = new SmtpClient();
        // Dev SMTP (Mailpit): auth'suz, TLS'siz.
        await client.ConnectAsync(options.Host, options.Port, SecureSocketOptions.None, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        return $"sent:{message.MessageId}";
    }
}