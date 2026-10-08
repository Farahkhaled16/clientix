using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Agency;

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly Translator _t;
    public IndexModel(FirestoreService fs, Translator t) { _fs = fs; _t = t; }

    public string AgencyId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public Portfolio P { get; set; } = new();
    public List<Meeting> Upcoming { get; set; } = new();
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public async Task OnGetAsync()
    {
        P = await _fs.GetPortfolio(AgencyId)
            ?? new Portfolio { AgencyId = AgencyId, CompanyName = User.Identity?.Name ?? "" };

        var now = DateTime.UtcNow;
        Upcoming = (await _fs.GetMeetings())
            .Where(m => m.AgencyId == AgencyId && m.Status == "approved" && m.StartsAt > now.AddMinutes(-30))
            .OrderBy(m => m.StartsAt).ToList();
    }
}