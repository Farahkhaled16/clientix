using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Notifications;

public class IndexModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly Translator _t;
    public IndexModel(FirestoreService fs, Translator t) { _fs = fs; _t = t; }

    public List<AppNotification> Items { get; set; } = new();
    public CultureInfo Cult => new(_t.Lang == "ar" ? "ar-EG" : "en-US");

    public async Task OnGetAsync()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Items = await _fs.GetUserNotifications(uid);   // بنعرضها بحالتها
        await _fs.MarkUserRead(uid);                   // وبعدين نعلّمها مقروءة
    }
}