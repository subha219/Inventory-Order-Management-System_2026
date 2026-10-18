namespace Application.Common.Services.EmailManager;

/// <summary>A file sent with an e-mail.</summary>
public class EmailAttachment
{
    public string FileName { get; init; } = string.Empty;
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/octet-stream";
}

public interface IEmailService
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);

    /// <summary>
    /// Sends an e-mail with optional attachments. Unlike <see cref="SendEmailAsync"/>, a problem (SMTP not set up,
    /// bad login, refused address) is thrown so the caller can show it instead of assuming the mail went out.
    /// </summary>
    Task SendEmailWithAttachmentsAsync(string email, string subject, string htmlMessage, IReadOnlyList<EmailAttachment>? attachments = null);
}
