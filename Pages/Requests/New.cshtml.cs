using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Requests;

public class NewModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly NotifyService _notify;
    private readonly Translator _t;
    public NewModel(FirestoreService fs, NotifyService notify, Translator t) { _fs = fs; _notify = notify; _t = t; }

    public HashSet<string> Selected { get; set; } = new();
    public string Budget { get; set; } = "unsure";
    public string Timeline { get; set; } = "month";
    public string City { get; set; } = "";
    public string Details { get; set; } = "";
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(List<string>? services, string? budget, string? timeline,
                                                 string? city, string? details)
    {
        Selected = (services ?? new()).Where(s => Catalog.Services.Any(c => c.Key == s)).ToHashSet();
        Budget = Catalog.Budgets.Any(b => b.Key == budget) ? budget! : "unsure";
        Timeline = Catalog.Timelines.Any(t => t.Key == timeline) ? timeline! : "month";
        City = (city ?? "").Trim();
        if (City.Length > 80) City = City[..80];
        Details = (details ?? "").Trim();

        if (Selected.Count == 0) { Error = "req.err.services"; return Page(); }
        if (Details.Length < 20 || Details.Length > 3000) { Error = "req.err.details"; return Page(); }

        var r = new BriefRequest
        {
            BusinessId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            BusinessName = User.Identity?.Name ?? "",
            BusinessEmail = User.FindFirstValue(ClaimTypes.Email) ?? "",
            Lang = _t.Lang,
            Services = string.Join(",", Selected),
            Budget = Budget,
            Timeline = Timeline,
            City = City,
            Details = Details
        };
        await _fs.CreateRequest(r);

        await _notify.Push("request",
            $"{r.BusinessName}: {Catalog.ServiceLabels(r.Services, _t.Lang)}",
            "/Broker/Requests?id=" + r.Id);

        return RedirectToPage("/Requests/Mine", new { sent = true });
    }
}