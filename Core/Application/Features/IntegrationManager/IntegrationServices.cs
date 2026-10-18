using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Features.IntegrationManager;

/// <summary>Names of the events that webhooks can subscribe to.</summary>
public static class WebhookEvents
{
    public const string ShipmentShipped = "shipment.shipped";
    public const string PortalResponse = "portal.response";
    public const string AutomationAlert = "automation.alert";
    public const string Test = "webhook.test";
    public static readonly string[] All = { ShipmentShipped, PortalResponse, AutomationAlert };
}

/// <summary>Creates and checks API keys. Only a SHA-256 hash of a key is stored.</summary>
public class ApiKeyService
{
    private readonly ICommandRepository<IntegrationApiKey> _keys;
    private readonly IUnitOfWork _unitOfWork;

    public ApiKeyService(ICommandRepository<IntegrationApiKey> keys, IUnitOfWork unitOfWork)
    {
        _keys = keys; _unitOfWork = unitOfWork;
    }

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    public static string NewRandomHex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    public async Task<bool> ValidateAsync(string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 100) return false;
        var hash = Hash(raw.Trim());
        var key = _keys.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(k => k.KeyHash == hash && k.IsActive == true);
        if (key == null) return false;
        if (key.LastUsedAtUtc == null || key.LastUsedAtUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            key.LastUsedAtUtc = DateTime.UtcNow;
            _keys.Update(key);
            await _unitOfWork.SaveAsync(ct);
        }
        return true;
    }
}

/// <summary>Sends signed JSON notifications to the active webhook endpoints. Never throws.</summary>
public class WebhookDispatcher
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly ICommandRepository<WebhookEndpoint> _hooks;
    private readonly IUnitOfWork _unitOfWork;

    public WebhookDispatcher(ICommandRepository<WebhookEndpoint> hooks, IUnitOfWork unitOfWork)
    {
        _hooks = hooks; _unitOfWork = unitOfWork;
    }

    private static bool Matches(string? events, string name)
    {
        if (string.IsNullOrWhiteSpace(events)) return false;
        var list = events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return list.Contains("*") || list.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    public static string BuildBody(string eventName, object data) =>
        JsonSerializer.Serialize(new { @event = eventName, occurredAtUtc = DateTime.UtcNow, data });

    /// <summary>Posts one body to one endpoint and returns a short status text.</summary>
    public static async Task<string> PostAsync(WebhookEndpoint hook, string eventName, string body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, hook.Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            var sig = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(hook.Secret ?? ""), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            req.Headers.Add("X-WHMS-Event", eventName);
            req.Headers.Add("X-WHMS-Signature", "sha256=" + sig);
            using var res = await Http.SendAsync(req, ct);
            return ((int)res.StatusCode) + " " + res.ReasonPhrase;
        }
        catch (Exception ex)
        {
            var m = ex.Message;
            return "Failed: " + (m.Length > 200 ? m[..200] : m);
        }
    }

    public async Task DispatchAsync(string eventName, object data, CancellationToken ct = default)
    {
        try
        {
            var hooks = _hooks.GetQuery().ApplyIsDeletedFilter().Where(h => h.IsActive == true).ToList()
                .Where(h => Matches(h.Events, eventName) && !string.IsNullOrWhiteSpace(h.Url)).ToList();
            if (hooks.Count == 0) return;
            var body = BuildBody(eventName, data);
            var results = await Task.WhenAll(hooks.Select(h => PostAsync(h, eventName, body, ct)));
            for (var i = 0; i < hooks.Count; i++)
            {
                hooks[i].LastStatus = results[i];
                hooks[i].LastAtUtc = DateTime.UtcNow;
                _hooks.Update(hooks[i]);
            }
            await _unitOfWork.SaveAsync(ct);
        }
        catch
        {
            // a broken webhook must never break the business action that triggered it
        }
    }
}
