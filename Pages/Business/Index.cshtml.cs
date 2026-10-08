using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Business;

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly Translator _t;
    public IndexModel(FirestoreService fs, Translator t) { _fs = fs; _t = t; }

    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Total { get; set; }
    public int AgencyTotal { get; set; }
    public List<Meeting> Upcoming { get; set; } = new();
    public List<Meeting> Pending { get; set; } = new();
    public List<Meeting> History { get; set; } = new();
    public Meeting? Next { get; set; }
    public string Countdown { get; set; } = "";
    public List<BrokerHub.Pages.Agencies.AgencyCard> Agencies { get; set; } = new();
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public async Task OnGetAsync()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Name = User.Identity?.Name ?? "";
        Email = User.FindFirstValue(ClaimTypes.Email) ?? "";

        var all = await _fs.GetMeetingsByBusiness(uid);   // مرتبة من الأحدث للأقدم
        var now = DateTime.UtcNow;
        Total = all.Count;

        Upcoming = all.Where(m => m.Status == "approved" && m.StartsAt > now.AddMinutes(-30))
                      .OrderBy(m => m.StartsAt).ToList();
        Pending = all.Where(m => m.Status == "pending" && m.StartsAt > now.AddHours(-1))
                     .OrderBy(m => m.StartsAt).ToList();
        History = all.Where(m => !Upcoming.Contains(m) && !Pending.Contains(m)).Take(5).ToList();

        Next = Upcoming.FirstOrDefault();
        if (Next != null)
        {
            var diff = Next.StartsAt - now;
            if (diff.TotalMinutes <= 1) Countdown = _t["bz.now"];
            else if (diff.TotalHours < 1) Countdown = $"{_t["bz.in"]} {(int)diff.TotalMinutes} {_t["bz.min"]}";
            else if (diff.TotalDays < 1) Countdown = $"{_t["bz.in"]} {(int)diff.TotalHours} {_t["bz.hours"]}";
            else Countdown = $"{_t["bz.in"]} {(int)diff.TotalDays} {_t["bz.days"]}";
        }

        var agencies = await _fs.GetAgencies();
        AgencyTotal = agencies.Count;
        var pf = (await _fs.GetAllPortfolios()).ToDictionary(p => p.AgencyId);
        Agencies = agencies.Take(8).Select(a => new BrokerHub.Pages.Agencies.AgencyCard
        {
            Id = a.Id,
            Name = a.Name,
            P = pf.TryGetValue(a.Id, out var p) ? p : new Portfolio()
        }).ToList();
    }
}