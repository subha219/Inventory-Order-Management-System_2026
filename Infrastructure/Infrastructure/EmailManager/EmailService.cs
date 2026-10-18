using Application.Common.Services.EmailManager;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Infrastructure.EmailManager;


public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly SmtpSettingsStore _store;

    public EmailService(ILogger<EmailService> logger, SmtpSettingsStore store)
    {
        _logger = logger;
        _store = store;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            await SendEmailWithAttachmentsAsync(email, subject, htmlMessage, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email.");
        }
    }

    public async Task SendEmailWithAttachmentsAsync(string email, string subject, string htmlMessage, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        // Read on every send, so a change saved on the Email Settings page applies immediately.
        var smtp = _store.Resolve();
        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException("E-mail is not set up yet. An administrator can fill it in under Users > Email Settings.");

        var fromAddress = string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.UserName : smtp.FromAddress;
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("E-mail needs a From address (or a user name that is an e-mail address). Fill it in under Users > Email Settings.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(smtp.FromName) ? "noreply" : smtp.FromName, fromAddress));
        message.To.Add(new MailboxAddress(email, email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlMessage
        };
        foreach (var a in attachments ?? Array.Empty<EmailAttachment>())
            bodyBuilder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));

        message.Body = bodyBuilder.ToMessageBody();

        var security = (smtp.Security ?? "Auto").ToLowerInvariant() switch
        {
            "starttls" => SecureSocketOptions.StartTls,
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto
        };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        client.Timeout = 20000;
        await client.ConnectAsync(smtp.Host, smtp.Port == 0 ? 587 : smtp.Port, security);
        if (!string.IsNullOrWhiteSpace(smtp.UserName))
            await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
