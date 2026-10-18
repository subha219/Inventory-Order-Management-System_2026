using System.Security.Cryptography;
using System.Text.Json;
using Application.Common.Services.EmailManager;
using Microsoft.Extensions.Options;

namespace Infrastructure.EmailManager;

/// <summary>
/// Keeps the SMTP settings an administrator saves in the app (App_Data/smtp-settings.json, outside wwwroot so it is never served).
/// The password is stored encrypted with a key kept beside it. Until something is saved, the SmtpSettings section of appsettings.json is used.
/// </summary>
public class SmtpSettingsStore : ISmtpSettingsStore
{
    private class Stored
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? UserName { get; set; }
        public string? PasswordEnc { get; set; }
        public string? FromAddress { get; set; }
        public string? FromName { get; set; }
        public string? Security { get; set; }
    }

    private static readonly object Gate = new();
    private readonly IOptionsMonitor<SmtpSettings> _fallback;
    private readonly string _dir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data");
    private string FilePath => Path.Combine(_dir, "smtp-settings.json");
    private string KeyPath => Path.Combine(_dir, "smtp.key");

    public SmtpSettingsStore(IOptionsMonitor<SmtpSettings> fallback)
    {
        _fallback = fallback;
    }

    private Stored? Read()
    {
        lock (Gate)
        {
            if (!File.Exists(FilePath)) return null;
            try { return JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath)); }
            catch { return null; }
        }
    }

    /// <summary>The settings to send mail with right now.</summary>
    public SmtpSettings Resolve()
    {
        var s = Read();
        if (s == null) return _fallback.CurrentValue;
        return new SmtpSettings
        {
            Host = s.Host, Port = s.Port, UserName = s.UserName, FromAddress = s.FromAddress, FromName = s.FromName,
            Security = s.Security, Password = Decrypt(s.PasswordEnc)
        };
    }

    public SmtpSettingsView GetView()
    {
        var s = Read();
        if (s != null)
            return new SmtpSettingsView
            {
                Host = s.Host, Port = s.Port == 0 ? 587 : s.Port, UserName = s.UserName, FromAddress = s.FromAddress, FromName = s.FromName,
                Security = string.IsNullOrWhiteSpace(s.Security) ? "Auto" : s.Security, HasPassword = !string.IsNullOrEmpty(s.PasswordEnc), Source = "Saved here"
            };
        var f = _fallback.CurrentValue;
        var configured = !string.IsNullOrWhiteSpace(f.Host);
        return new SmtpSettingsView
        {
            Host = f.Host, Port = f.Port == 0 ? 587 : f.Port, UserName = f.UserName, FromAddress = f.FromAddress, FromName = f.FromName,
            Security = string.IsNullOrWhiteSpace(f.Security) ? "Auto" : f.Security, HasPassword = !string.IsNullOrEmpty(f.Password),
            Source = configured ? "appsettings.json" : "Not set up"
        };
    }

    public void Save(SmtpSettingsInput input)
    {
        var allowed = new[] { "Auto", "StartTls", "SslOnConnect", "None" };
        var security = allowed.FirstOrDefault(a => string.Equals(a, input.Security, StringComparison.OrdinalIgnoreCase)) ?? "Auto";
        if (input.Port <= 0 || input.Port > 65535) throw new InvalidOperationException("Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(input.Host)) throw new InvalidOperationException("Server (host) is required.");

        var existing = Read();
        string? enc;
        if (input.ClearPassword) enc = null;
        else if (!string.IsNullOrEmpty(input.Password)) enc = Encrypt(input.Password);
        else if (existing != null) enc = existing.PasswordEnc;
        else enc = string.IsNullOrEmpty(_fallback.CurrentValue.Password) ? null : Encrypt(_fallback.CurrentValue.Password!);

        var stored = new Stored
        {
            Host = input.Host.Trim(), Port = input.Port, UserName = input.UserName?.Trim(), PasswordEnc = enc,
            FromAddress = input.FromAddress?.Trim(), FromName = input.FromName?.Trim(), Security = security
        };
        lock (Gate)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private byte[] Key()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(_dir);
            if (File.Exists(KeyPath))
            {
                var k = Convert.FromBase64String(File.ReadAllText(KeyPath).Trim());
                if (k.Length == 32) return k;
            }
            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(KeyPath, Convert.ToBase64String(key));
            return key;
        }
    }

    private string Encrypt(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = System.Text.Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(Key(), 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    private string? Decrypt(string? enc)
    {
        if (string.IsNullOrEmpty(enc)) return null;
        try
        {
            var all = Convert.FromBase64String(enc);
            var nonce = all[..12];
            var tag = all[12..28];
            var cipher = all[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(Key(), 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }
}
