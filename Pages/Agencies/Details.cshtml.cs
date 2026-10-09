using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Agencies;

public class DetailsModel : PageModel
{
    private readonly FirestoreService _fs;
    public DetailsModel(FirestoreService fs) => _fs = fs;

    public AppUser? Agency { get; set; }     // بنسيب الاسم زي ما هو عشان الصفحة مش تتغير
    public Portfolio P { get; set; } = new();

    public async Task OnGetAsync(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var pf = await _fs.GetPortfolio(id);
        if (pf == null || string.IsNullOrWhiteSpace(pf.CompanyName)) return;

        P = pf;
        Agency = new AppUser { Id = id, Name = pf.CompanyName, Role = "agency" };
    }
}