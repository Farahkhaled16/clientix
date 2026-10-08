using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Chat;

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly Translator _t;
    public IndexModel(FirestoreService fs, Translator t) { _fs = fs; _t = t; }

    public string ThreadId { get; set; } = "";
    public List<ChatMessage> Messages { get; set; } = new();
    public string? About { get; set; }
    public string Prefill { get; set; } = "";

    public async Task OnGetAsync(string? about)
    {
        ThreadId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Messages = await _fs.GetMessages(ThreadId);
        await _fs.MarkThreadRead(ThreadId, false);

        if (!string.IsNullOrEmpty(about))
        {
            var a = await _fs.GetUserById(about);
            if (a != null && a.Role == "agency")
            {
                var pf = await _fs.GetPortfolio(a.Id);
                About = string.IsNullOrEmpty(pf?.CompanyName) ? a.Name : pf!.CompanyName;
                Prefill = $"{_t["chat.prefill"]} {About}";
            }
        }
    }
}