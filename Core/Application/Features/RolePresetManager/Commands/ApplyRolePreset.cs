using Application.Common.Services.SecurityManager;
using Application.Features.RolePresetManager.Queries;
using MediatR;
using System.Security.Cryptography;

namespace Application.Features.RolePresetManager.Commands;

public class ApplyRolePresetResult
{
    public string? Email { get; init; }
    public string? Password { get; init; }
    public bool Created { get; init; }
    public List<string> Roles { get; init; } = new();
    public string? Message { get; init; }
}

public class ApplyRolePresetRequest : IRequest<ApplyRolePresetResult>
{
    public string? PresetKey { get; init; }
    /// <summary>Existing user to receive the preset roles. Ignored when CreateTestUser is true.</summary>
    public string? UserId { get; init; }
    public bool CreateTestUser { get; init; }
    public string? CreatedById { get; init; }
}

public class ApplyRolePresetHandler : IRequestHandler<ApplyRolePresetRequest, ApplyRolePresetResult>
{
    private readonly ISecurityService _security;

    public ApplyRolePresetHandler(ISecurityService security)
    {
        _security = security;
    }

    private static string NewPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var sb = new System.Text.StringBuilder("Wh@");
        for (var i = 0; i < 10; i++) sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        sb.Append("9aA!");
        return sb.ToString();
    }

    public async Task<ApplyRolePresetResult> Handle(ApplyRolePresetRequest request, CancellationToken cancellationToken)
    {
        var preset = RolePresetCatalog.All.FirstOrDefault(p => p.Key == request.PresetKey);
        if (string.IsNullOrEmpty(preset.Key)) throw new Exception("Unknown preset.");

        var roleNames = (await _security.GetRoleListAsync(cancellationToken))
            .Select(r => r.Name ?? "").Where(n => n != "").ToList();
        // A preset is a real role (AspNetRoles) named after the job; signing in expands it to its menu-area roles.
        var wanted = roleNames.Where(n => string.Equals(n, preset.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (wanted.Count == 0) throw new Exception("The role '" + preset.Name + "' does not exist yet. Restart the application once so it is created.");

        string userId;
        string email;
        string? password = null;
        var created = false;

        if (request.CreateTestUser)
        {
            email = preset.Key + ".test@example.com";
            password = NewPassword();
            var users = await _security.GetUserListAsync(cancellationToken);
            var existing = users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                var res = await _security.CreateUserAsync(email, password, password, preset.Name, "Test", true, false, false, request.CreatedById ?? "", cancellationToken);
                userId = res.UserId ?? throw new Exception("Could not create the test user.");
                created = true;
            }
            else
            {
                userId = existing.Id ?? throw new Exception("Test user has no id.");
                await _security.UpdatePasswordUserAsync(userId, password, cancellationToken);
            }

            // A test user holds exactly the preset roles (plus Profiles).
            var current = await _security.GetUserRolesAsync(userId, cancellationToken);
            foreach (var r in current)
            {
                if (string.Equals(r, "Profiles", StringComparison.OrdinalIgnoreCase)) continue;
                if (!wanted.Contains(r, StringComparer.OrdinalIgnoreCase))
                    await _security.UpdateUserRoleAsync(userId, r, false, cancellationToken);
            }
        }
        else
        {
            if (string.IsNullOrEmpty(request.UserId)) throw new Exception("Choose a user.");
            var users = await _security.GetUserListAsync(cancellationToken);
            var u = users.FirstOrDefault(x => x.Id == request.UserId) ?? throw new Exception("User not found.");
            userId = u.Id!;
            email = u.Email ?? "";
        }

        foreach (var r in wanted)
            await _security.UpdateUserRoleAsync(userId, r, true, cancellationToken);

        var finalRoles = await _security.GetUserRolesAsync(userId, cancellationToken);
        return new ApplyRolePresetResult
        {
            Email = email,
            Password = password,
            Created = created,
            Roles = finalRoles,
            Message = request.CreateTestUser ? "Test user is ready." : "Roles applied."
        };
    }
}
