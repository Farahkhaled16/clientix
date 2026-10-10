using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Broker;

public class RequestsModel : PageModel
{
    public record Cand(string Id, string Name, string City, string Services, int Score, bool Checked);

    private readonly FirestoreService _fs;
    private readonly NotifyService _notify;
    private readonly EmailService _mail;
    private readonly Translator _t;
    public RequestsModel(FirestoreService fs, NotifyService notify, EmailService mail, Translator t)
    { _fs = fs; _notify = notify; _mail = mail; _t = t; }

    [BindProperty(Name = "id", SupportsGet = true)] public string? Id { get; set; }
    public List<BriefRequest> Items { get; set; } = new();
    public BriefRequest? Current { get; set; }
    public List<Cand> Cands { get; set; } = new();
    public bool HasThread { get; set; }
    public string? Msg { get; set; }
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public string Labels(BriefRequest r) => Catalog.ServiceLabels(r.Services, _t.Lang);
    public string BudgetLabel(BriefRequest r) => Catalog.Label(Catalog.Budgets, r.Budget, _t.Lang);
    public string TimelineLabel(BriefRequest r) => Catalog.Label(Catalog.Timelines, r.Timeline, _t.Lang);

    private static List<string> Split(string? s) =>
        (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public async Task OnGetAsync(string? msg)
    {
        Msg = msg;
        Items = await _fs.GetRequests();
        if (string.IsNullOrEmpty(Id)) return;

        Current = Items.FirstOrDefault(r => r.Id == Id);
        if (Current == null) return;

        HasThread = await _fs.GetThread(Current.BusinessId) != null;

        var want = Split(Current.Services);
        int Score(Portfolio p)
        {
            var have = Split(p.Services);
            var s = want.Count(w => have.Any(h =>
                h.Contains(w, StringComparison.OrdinalIgnoreCase) || w.Contains(h, StringComparison.OrdinalIgnoreCase))) * 2;
            if (!string.IsNullOrEmpty(Current.City) &&
                string.Equals((p.City ?? "").Trim(), Current.City, StringComparison.OrdinalIgnoreCase)) s += 1;
            return s;
        }

        var scored = (await _fs.GetAllPortfolios())
            .Where(p => !string.IsNullOrWhiteSpace(p.CompanyName))
            .Select(p => (p, score: Score(p)))
            .OrderByDescending(x => x.score).ThenBy(x => x.p.CompanyName).ToList();

        var preselect = Current.Status == "matched"
            ? Current.Recommended.ToHashSet()
            : scored.Where(x => x.score > 0).Take(3).Select(x => x.p.AgencyId).ToHashSet();

        Cands = scored.Select(x => new Cand(x.p.AgencyId, x.p.CompanyName, x.p.City ?? "",
                                            x.p.Services ?? "", x.score, preselect.Contains(x.p.AgencyId))).ToList();
    }

    public async Task<IActionResult> OnPostRecommendAsync(string id, List<string>? agencyIds, string? note)
    {
        var r = await _fs.GetRequest(id);
        if (r == null) return RedirectToPage();

        var ids = (agencyIds ?? new()).Distinct().Take(6).ToList();
        if (ids.Count == 0) return RedirectToPage(new { id, msg = "req.pickone" });

        r.Recommended = ids;
        r.BrokerNote = (note ?? "").Trim();
        r.Status = "matched";
        r.MatchedAt = DateTime.UtcNow;
        await _fs.UpdateRequest(r);

        var lang = string.IsNullOrEmpty(r.Lang) ? "ar" : r.Lang;
        var body = $"{_t.Get("req.matched.body", lang)} ({ids.Count})";
        await _notify.PushUser(r.BusinessId, "recommend", body, "/Requests/Mine", lang);

        try
        {
            var mailBody = string.IsNullOrWhiteSpace(r.BrokerNote) ? body : $"{body}\n\n{r.BrokerNote}";
            await _mail.SendLocalized(r.BusinessEmail, _t.Get("req.matched.subj", lang),
                mailBody.Replace("\n", "<br>"), _t.Get("req.view", lang),
                $"{Request.Scheme}://{Request.Host}/Requests/Mine", lang);
        }
        catch { }

        return RedirectToPage(new { id, msg = "req.sent" });
    }

    public async Task<IActionResult> OnPostCloseAsync(string id)
    {
        var r = await _fs.GetRequest(id);
        if (r != null) { r.Status = "closed"; await _fs.UpdateRequest(r); }
        return RedirectToPage(new { id });
    }
}