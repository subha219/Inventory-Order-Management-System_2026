using Infrastructure.SecurityManager.AspNetIdentity;
using Infrastructure.SecurityManager.Roles;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.SeedManager.Demos;

public class UserSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserSeeder(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task GenerateDataAsync()
    {
        var userNames = new List<string>
        {
            "Alex", "Taylor", "Jordan", "Morgan", "Riley",
            "Casey", "Peyton", "Cameron", "Jamie", "Drew",
            "Dakota", "Avery", "Quinn", "Harper", "Rowan",
            "Emerson", "Finley", "Skyler", "Charlie", "Sage"
        };

        var defaultPassword = "Demo#12345";
        var domain = "@example.com";

        foreach (var name in userNames)
        {
            var email = $"{name.ToLower()}{domain}";

            if (await _userManager.FindByEmailAsync(email) == null)
            {
                var applicationUser = new ApplicationUser(email, name, "User")
                {
                    EmailConfirmed = true
                };

                await _userManager.CreateAsync(applicationUser, defaultPassword);
            }
        }
    }
}
