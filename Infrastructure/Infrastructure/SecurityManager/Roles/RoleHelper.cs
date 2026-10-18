using Application.Features.RolePresetManager.Queries;
using Infrastructure.SecurityManager.NavigationMenu;

namespace Infrastructure.SecurityManager.Roles;

public class RoleHelper
{
    public static List<string> GetAdminRoles()
    {
        var roles = new List<string>();
        roles = NavigationTreeStructure.GetCompleteFirstMenuNavigationSegment();
        return roles;
    }

    public const string AdministratorRole = "Administrator";

    /// <summary>The only roles stored in AspNetRoles: Administrator plus the job roles.</summary>
    public static List<string> GetTableRoles()
    {
        var roles = new List<string> { AdministratorRole };
        roles.AddRange(RolePresetCatalog.All.Select(p => p.Name));
        return roles;
    }

    //make sure or cross check with NavigationTreeStructure
    public static string GetProfileRole()
    {
        return "Profiles";
    }
}
