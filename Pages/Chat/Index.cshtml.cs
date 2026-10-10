using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Services;

namespace BrokerHub.Pages.Chat;

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly ChatService _chat;
    private readonly Translator _t;
    public IndexModel(FirestoreService fs, ChatService chat, Translator t) { _fs = fs; _chat = chat; _t = t; }

    public string ThreadId { get; set; } = "";
    public string InitJson { get; set; } = "[]";
    public string? About { get; set; }
    public string Prefill { get; set; } = "";

    public async Task OnGetAsync(string? about)
    {
        ThreadId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var th = await _fs.GetThread(ThreadId);
        var items = await _chat.History(ThreadId, "business", th);
        InitJson = JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await _fs.MarkThreadSeen(ThreadId, false);

        if (!string.IsNullOrEmpty(about))
        {
            var pf = await _fs.GetPortfolio(about);
            if (pf != null && !string.IsNullOrWhiteSpace(pf.CompanyName))
            {
                About = pf.CompanyName;
                Prefill = $"{_t["chat.prefill"]} {About}";
            }
        }
    }
}