using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Meetings;

public class MineModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly Translator _t;
    public MineModel(FirestoreService fs, Translator t) { _fs = fs; _t = t; }

    public List<Meeting> Items { get; set; } = new();
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public async Task OnGetAsync() =>
        Items = await _fs.GetMeetingsByBusiness(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public string GCal(Meeting m) => Ics.GoogleUrl(m, _t["meet.ics.title"],
        string.IsNullOrEmpty(m.AgencyName) ? _t["meet.general"] : m.AgencyName);
}