using Microsoft.AspNetCore.SignalR;
using BrokerHub.Models;

namespace BrokerHub.Services;

public class NotifyService
{
    private readonly FirestoreService _fs;
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly EmailService _mail;
    private readonly IConfiguration _c;
    private readonly Translator _t;
    private readonly IHttpContextAccessor _http;

    public NotifyService(FirestoreService fs, IHubContext<NotificationsHub> hub, EmailService mail,
                         IConfiguration c, Translator t, IHttpContextAccessor http)
    { _fs = fs; _hub = hub; _mail = mail; _c = c; _t = t; _http = http; }

    public async Task Push(string type, string body, string link = "/Broker#activity")
    {
        try
        {
            var n = new AppNotification { Type = type, Body = body, Link = link };
            await _fs.AddNotification(n);

            // لحظي: جرس + توست عند الـ Broker
            await _hub.Clients.Group("broker").SendAsync("notify",
                new { id = n.Id, type = n.Type, body = n.Body, link = n.Link });

            // إيميل للحاجات المهمة بس
            var to = _c["Broker:Email"];
            if (!string.IsNullOrEmpty(to) && (type == "register" || type == "meeting"))
            {
                var req = _http.HttpContext?.Request;
                var baseUrl = req == null ? "" : $"{req.Scheme}://{req.Host}";
                await _mail.SendCustom(to, _t["n." + type], body, _t["mail.open"], baseUrl + link);
            }
        }
        catch { /* الإشعار عمره ما يوقّف حاجة عند المستخدم */ }
    }
}