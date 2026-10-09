using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Services;

namespace BrokerHub.Pages.Calendar;

public class IndexModel : PageModel
{
    private readonly CalendarLinks _links;
    private readonly Translator _t;
    private readonly GoogleCalendarService _g;
    public IndexModel(CalendarLinks links, Translator t, GoogleCalendarService g) { _links = links; _t = t; _g = g; }

    public string HttpsUrl { get; set; } = "";
    public string WebcalUrl { get; set; } = "";
    public string GoogleUrl { get; set; } = "";
    public bool IsLocal { get; set; }
    public bool GoogleConnected { get; set; }
    public string? Flash { get; set; }

    public async Task OnGetAsync(string? gcal)
    {
        Flash = gcal;
        var role = User.IsInRole("broker") ? "broker" : User.IsInRole("agency") ? "agency" : "business";
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        GoogleConnected = await _g.IsConnected(id);

        var path = $"/cal/{_links.Token(role, id)}/clientix.ics?lang={_t.Lang}";
        HttpsUrl = $"{Request.Scheme}://{Request.Host}{path}";
        WebcalUrl = $"webcal://{Request.Host}{path}";
        GoogleUrl = "https://calendar.google.com/calendar/r?cid=" + Uri.EscapeDataString(WebcalUrl);
        IsLocal = Request.Host.Host is "localhost" or "127.0.0.1";
    }
}