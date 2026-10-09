using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BrokerHub.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.IsInRole("broker")) return Redirect("/Broker");
        if (User.IsInRole("business")) return Redirect("/Business");
        return Page();
    }
}