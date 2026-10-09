using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Broker;

public class AgencyRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Portfolio P { get; set; } = new();
}

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly NotifyService _notify;
    private readonly Translator _t;
    public IndexModel(FirestoreService fs, NotifyService notify, Translator t) { _fs = fs; _notify = notify; _t = t; }

    public List<AgencyRow> Items { get; set; } = new();
    public List<Meeting> Pending { get; set; } = new();
    public List<Meeting> Upcoming { get; set; } = new();
    public List<AppNotification> Feed { get; set; } = new();
    public int AgencyCount, BusinessCount, VisitsToday;
    public string? Msg { get; set; }
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public async Task OnGetAsync(string? msg)
    {
        Msg = msg;

        Items = (await _fs.GetAllPortfolios())
            .OrderBy(p => p.CompanyName)
            .Select(p => new AgencyRow { Id = p.AgencyId, Name = p.CompanyName, P = p })
            .ToList();
        AgencyCount = Items.Count;
        BusinessCount = await _fs.CountUsers("business");

        var now = DateTime.UtcNow;
        var meetings = await _fs.GetMeetings();
        Pending = meetings.Where(m => m.Status == "pending" && m.StartsAt > now.AddHours(-1)).ToList();
        Upcoming = meetings.Where(m => m.Status == "approved" && m.StartsAt > now.AddMinutes(-30)).ToList();

        var recent = await _fs.GetNotifications(200);
        Feed = recent.Take(40).ToList();
        var todayStart = AppTime.ToUtc(AppTime.ToLocal(now).Date);
        VisitsToday = recent.Count(n => n.Type == "visit" && n.CreatedAt >= todayStart);
    }

    public async Task<IActionResult> OnPostDecideAsync(string id, string decision)
    {
        if (decision is not ("approved" or "rejected" or "cancelled")) return RedirectToPage();

        var m = await _fs.GetMeeting(id);
        if (m == null || m.Status == decision) return RedirectToPage();

        if (decision == "approved")
        {
            var all = await _fs.GetMeetings();
            if (all.Any(x => x.Id != m.Id && x.Status == "approved" && Math.Abs((x.StartsAt - m.StartsAt).TotalMinutes) < 30))
                return RedirectToPage(new { msg = "meet.conflict" });

            var diff = m.StartsAt - DateTime.UtcNow;
            m.ReminderDaySent = diff <= TimeSpan.FromHours(24);
            m.ReminderHourSent = diff <= TimeSpan.FromHours(1);
        }

        m.Status = decision;
        await _fs.UpdateMeeting(m);
        await _notify.MeetingDecision(m, decision);

        return RedirectToPage(new { msg = "meet.updated" });
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        await _fs.MarkAllRead();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        await _fs.DeleteAgency(id);
        return RedirectToPage(new { msg = "bk.deleted" });
    }
}