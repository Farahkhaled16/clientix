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

    public async Task OnGetAsync()
    {
        var agencies = await _fs.GetAgencies();
        var portfolios = (await _fs.GetAllPortfolios()).ToDictionary(p => p.AgencyId);

        Items = agencies.Select(a => new AgencyCard
        {
            Id = a.Id,
            Name = a.Name,
            P = portfolios.TryGetValue(a.Id, out var p) ? p : new Portfolio()
        }).ToList();
    }
}