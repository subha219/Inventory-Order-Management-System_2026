namespace Application.Common.Services.EmailManager;

/// <summary>What the admin sees on the settings page. The password is never sent back to the browser.</summary>
public class SmtpSettingsView
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    /// <summary>Auto, StartTls, SslOnConnect or None.</summary>
    public string Security { get; set; } = "Auto";
    public bool HasPassword { get; set; }
    /// <summary>"Saved here", "appsettings.json" or "Not set up".</summary>
    public string Source { get; set; } = "Not set up";
}

/// <summary>What the admin submits. An empty Password keeps the saved one.</summary>
public class SmtpSettingsInput
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? Security { get; set; }
    public bool ClearPassword { get; set; }
}

public interface ISmtpSettingsStore
{
    SmtpSettingsView GetView();
    void Save(SmtpSettingsInput input);
}
