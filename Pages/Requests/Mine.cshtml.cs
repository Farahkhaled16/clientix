using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Requests;

public class MineModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly Translator _t;
    public MineModel(FirestoreService fs, Translator t) { _fs = fs; _t = t; }

    public List<BriefRequest> Items { get; set; } = new();
    public Dictionary<string, Portfolio> Pf { get; set; } = new();
    public bool Sent { get; set; }
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public string Labels(BriefRequest r) => Catalog.ServiceLabels(r.Services, _t.Lang);
    public string Budget(BriefRequest r) => Catalog.Label(Catalog.Budgets, r.Budget, _t.Lang);
    public string Timeline(BriefRequest r) => Catalog.Label(Catalog.Timelines, r.Timeline, _t.Lang);

    public async Task OnGetAsync(bool sent = false)
    {
        Sent = sent;
        Items = await _fs.GetRequestsByBusiness(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (Items.Any(r => r.Recommended.Count > 0))
            Pf = (await _fs.GetAllPortfolios()).ToDictionary(p => p.AgencyId);
    }
}