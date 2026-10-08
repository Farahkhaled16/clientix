using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Agencies;

public class DetailsModel : PageModel
{
    private readonly FirestoreService _fs;
    public DetailsModel(FirestoreService fs) => _fs = fs;

    public AppUser? Agency { get; set; }
    public Portfolio P { get; set; } = new();

    public async Task OnGetAsync(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var user = await _fs.GetUserById(id);
        if (user == null || user.Role != "agency" || !user.EmailConfirmed) return;
        Agency = user;
        P = await _fs.GetPortfolio(id) ?? new Portfolio();
    }
}