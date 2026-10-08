using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Meetings;

public class BookModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly NotifyService _notify;
    public BookModel(FirestoreService fs, NotifyService notify) { _fs = fs; _notify = notify; }

    [BindProperty(SupportsGet = true)] public string? AgencyId { get; set; }
    public string? AgencyName { get; set; }
    public string? Error { get; set; }
    public bool Sent { get; set; }
    public string MinLocal => AppTime.ToLocal(DateTime.UtcNow.AddMinutes(30)).ToString("yyyy-MM-ddTHH:mm");

    private async Task LoadAgency()
    {
        if (string.IsNullOrEmpty(AgencyId)) return;
        var a = await _fs.GetUserById(AgencyId);
        if (a != null && a.Role == "agency")
        {
            var pf = await _fs.GetPortfolio(a.Id);
            AgencyName = string.IsNullOrEmpty(pf?.CompanyName) ? a.Name : pf!.CompanyName;
        }
    }

    public async Task OnGetAsync() => await LoadAgency();

    public async Task<IActionResult> OnPostAsync(DateTime slot, string? note)
    {
        await LoadAgency();
        var utc = AppTime.ToUtc(slot);

        if (utc < DateTime.UtcNow.AddMinutes(30)) { Error = "meet.past"; return Page(); }

        var all = await _fs.GetMeetings();
        if (all.Any(m => m.Status == "approved" && Math.Abs((m.StartsAt - utc).TotalMinutes) < 30))
        { Error = "meet.busy"; return Page(); }

        var meeting = new Meeting
        {
            BusinessId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            BusinessName = User.Identity!.Name ?? "",
            BusinessEmail = User.FindFirstValue(ClaimTypes.Email) ?? "",
            AgencyId = AgencyId ?? "",
            AgencyName = AgencyName ?? "",
            Note = note ?? "",
            StartsAt = utc
        };
        await _fs.CreateMeeting(meeting);

        await _notify.Push("meeting",
            $"{meeting.BusinessName} / {(AgencyName ?? "-")} / {AppTime.ToLocal(utc):d MMM, h:mm tt}");

        Sent = true;
        return Page();
    }
}