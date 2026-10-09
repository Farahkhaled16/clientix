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
        Items = (await _fs.GetAllPortfolios())
            .Where(p => !string.IsNullOrWhiteSpace(p.CompanyName))
            .OrderBy(p => p.CompanyName)
            .Select(p => new AgencyCard { Id = p.AgencyId, Name = p.CompanyName, P = p })
            .ToList();
    }
}