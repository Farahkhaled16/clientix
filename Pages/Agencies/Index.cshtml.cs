using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Agencies;

public class AgencyCard
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Portfolio P { get; set; } = new();
}

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    public IndexModel(FirestoreService fs) => _fs = fs;

    public List<AgencyCard> Items { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> TopServices { get; set; } = new();
    public string? Q { get; set; }
    public string? City { get; set; }
    public string? Service { get; set; }
    public int Total { get; set; }
    public bool Filtered =>
        !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(City) || !string.IsNullOrWhiteSpace(Service);

    private static IEnumerable<string> Split(string? s) =>
        (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsOn(string s) => string.Equals(Service, s, StringComparison.OrdinalIgnoreCase);

    public string Link(string? service)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add("q=" + Uri.EscapeDataString(Q));
        if (!string.IsNullOrWhiteSpace(City)) parts.Add("city=" + Uri.EscapeDataString(City));
        if (!string.IsNullOrWhiteSpace(service)) parts.Add("service=" + Uri.EscapeDataString(service));
        return "/Agencies" + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
    }

    public async Task OnGetAsync(string? q, string? city, string? service)
    {
        Q = q?.Trim(); City = city?.Trim(); Service = service?.Trim();

        var all = (await _fs.GetAllPortfolios())
            .Where(p => !string.IsNullOrWhiteSpace(p.CompanyName)).ToList();
        Total = all.Count;

        Cities = all.Select(p => (p.City ?? "").Trim()).Where(c => c.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();

        TopServices = all.SelectMany(p => Split(p.Services))
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Take(12).Select(g => g.Key).ToList();

        IEnumerable<Portfolio> res = all;
        if (!string.IsNullOrWhiteSpace(Q))
            res = res.Where(p => new[] { p.CompanyName, p.About, p.Services, p.City }
                .Any(f => (f ?? "").Contains(Q, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(City))
            res = res.Where(p => string.Equals((p.City ?? "").Trim(), City, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(Service))
            res = res.Where(p => Split(p.Services).Any(s => string.Equals(s, Service, StringComparison.OrdinalIgnoreCase)));

        Items = res.OrderBy(p => p.CompanyName)
                   .Select(p => new AgencyCard { Id = p.AgencyId, Name = p.CompanyName, P = p }).ToList();
    }
}