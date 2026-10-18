using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace InventraUI.FrontEnd.Pages.Dashboards;

/// <summary>Works out which dashboard a signed-in user belongs on, from the role claims in the sign-in cookie.</summary>
public static class DashboardRouter
{
    public static string PersonaOf(ClaimsPrincipal user)
    {
        bool Has(string r) => user.Claims.Any(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, r, StringComparison.OrdinalIgnoreCase));
        if (Has("Administrator") || Has("Users")) return "admin";
        if (Has("Manager")) return "manager";
        if (Has("Auditor / Read-only")) return "auditor";
        if (Has("Finance / Accountant")) return "finance";
        if (Has("Warehouse Supervisor")) return "supervisor";
        if (Has("Warehouse Operator")) return "operator";
        if (Has("Quality / Returns Inspector")) return "quality";
        if (Has("Inventory Controller")) return "controller";
        if (Has("Buyer / Purchasing")) return "buyer";
        if (Has("Customer Service")) return "service";
        if (Has("Sales")) return "sales";
        // Accounts that still hold the older menu-area roles
        if (Has("Warehouses")) return "operator";
        if (Has("Dashboards")) return "manager";
        if (Has("PurchaseOrders")) return "buyer";
        if (Has("SalesOrders")) return "sales";
        if (Has("Products")) return "controller";
        return "general";
    }

    public static string PathFor(string persona) => persona switch
    {
        "admin" => "/Dashboards/BusinessAnalytics",
        "manager" => "/Dashboards/ManagerDashboard",
        "finance" => "/Dashboards/FinanceDashboard",
        "operator" => "/Dashboards/WarehouseDashboard",
        "supervisor" => "/Dashboards/SupervisorDashboard",
        "quality" => "/Dashboards/QualityDashboard",
        "service" => "/Dashboards/ServiceDashboard",
        "auditor" => "/Dashboards/AuditorDashboard",
        "controller" => "/Dashboards/InventoryDashboard",
        "buyer" => "/Dashboards/PurchasingDashboard",
        "sales" => "/Dashboards/SalesDashboard",
        _ => "/Dashboards/MyDashboard"
    };
}

/// <summary>Base for the role dashboards: anyone who lands on the wrong one is sent to their own.</summary>
public abstract class PersonaPageModel : PageModel
{
    protected abstract string MyPath { get; }

    public IActionResult OnGet()
    {
        var target = DashboardRouter.PathFor(DashboardRouter.PersonaOf(User));
        return target == MyPath ? Page() : Redirect(target);
    }
}

/// <summary>The "Dashboard" menu entry: it only forwards each person to the dashboard of their job.</summary>
public class DefaultDashboardModel : PageModel
{
    public IActionResult OnGet() => Redirect(DashboardRouter.PathFor(DashboardRouter.PersonaOf(User)));
}

/// <summary>The long-form charts page. Managers, finance and administrators may open it.</summary>
public class BusinessAnalyticsModel : PageModel
{
    public IActionResult OnGet()
    {
        var persona = DashboardRouter.PersonaOf(User);
        return persona is "manager" or "finance" or "admin" or "auditor" ? Page() : Redirect(DashboardRouter.PathFor(persona));
    }
}

public class ManagerDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/ManagerDashboard"; }
public class FinanceDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/FinanceDashboard"; }
/// <summary>The administrator's system overview (users, approvals, audit). Administrators land on Business analytics; this page stays one click away.</summary>
public class AdminDashboardModel : PageModel
{
    public IActionResult OnGet() => DashboardRouter.PersonaOf(User) == "admin" ? Page() : Redirect(DashboardRouter.PathFor(DashboardRouter.PersonaOf(User)));
}
public class WarehouseDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/WarehouseDashboard"; }
public class InventoryDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/InventoryDashboard"; }
public class PurchasingDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/PurchasingDashboard"; }
public class SalesDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/SalesDashboard"; }
public class SupervisorDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/SupervisorDashboard"; }
public class QualityDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/QualityDashboard"; }
public class ServiceDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/ServiceDashboard"; }
public class AuditorDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/AuditorDashboard"; }
public class MyDashboardModel : PersonaPageModel { protected override string MyPath => "/Dashboards/MyDashboard"; }
