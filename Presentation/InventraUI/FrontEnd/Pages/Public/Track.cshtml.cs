using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;

namespace InventraUI.FrontEnd.Pages.Public;

public class TrackModel : PageModel
{
    /// <summary>The link token, kept to letters and digits so it is safe to write into the page.</summary>
    public string Token { get; private set; } = string.Empty;

    public void OnGet(string token)
    {
        Token = Regex.IsMatch(token ?? "", "^[A-Za-z0-9]{8,64}$") ? token! : string.Empty;
    }
}
