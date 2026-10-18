using Application.Features.RolePresetManager.Queries;
using Infrastructure.SecurityManager.AspNetIdentity;
using Infrastructure.SecurityManager.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.SeedManager.Demos;

/// <summary>Creates one demo login per job type (idempotent, runs on every start). Delete these users before go-live.</summary>
public class RolePresetUserSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IdentitySettings _identitySettings;
    private readonly IConfiguration _configuration;

    public RolePresetUserSeeder(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IOptions<IdentitySettings> identitySettings, IConfiguration configuration)
    {
        _configuration = configuration;
        _identitySettings = identitySettings.Value;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task GenerateDataAsync()
    {
        var defaultPassword = "Demo#12345";
        // Only Administrator and the job roles live in the AspNetRoles table.
        foreach (var roleName in RoleHelper.GetTableRoles())
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }

        var admin = await _userManager.FindByEmailAsync(_identitySettings.DefaultAdmin.Email);
        if (admin != null && !await _userManager.IsInRoleAsync(admin, RoleHelper.AdministratorRole))
            await _userManager.AddToRoleAsync(admin, RoleHelper.AdministratorRole);

        // Set "CreateDemoLogins": false in appsettings.json for a real client: roles are still created, test logins are not.
        if (!(_configuration.GetValue<bool?>("CreateDemoLogins") ?? true)) return;

        foreach (var p in RolePresetCatalog.All)
        {
            var email = p.Key + ".test@example.com";
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser(email, p.Name, "Test") { EmailConfirmed = true };
                var created = await _userManager.CreateAsync(user, defaultPassword);
                if (!created.Succeeded) continue;
            }

            var wanted = new[] { p.Name }.Distinct();
            foreach (var role in wanted)
            {
                if (await _roleManager.RoleExistsAsync(role) && !await _userManager.IsInRoleAsync(user, role))
                    await _userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
