using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using BrokerHub.Models;

namespace BrokerHub.Services;

public class NotifyService
{
    private readonly FirestoreService _fs;
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly IHubContext<UserHub> _userHub;
    private readonly EmailService _mail;
    private readonly IConfiguration _c;
    private readonly Translator _t;
    private readonly IHttpContextAccessor _http;

    public NotifyService(FirestoreService fs, IHubContext<NotificationsHub> hub, IHubContext<UserHub> userHub,
                         EmailService mail, IConfiguration c, Translator t, IHttpContextAccessor http)
    { _fs = fs; _hub = hub; _userHub = userHub; _mail = mail; _c = c; _t = t; _http = http; }

    private string BaseUrl()
    {
        var req = _http.HttpContext?.Request;
        if (req != null) return $"{req.Scheme}://{req.Host}";
        return (_c["App:BaseUrl"] ?? "").TrimEnd('/');
    }

    private static CultureInfo Cult(string lang) => new(lang == "en" ? "en-US" : "ar-EG");

    // ---------- إشعارات الـ Broker ----------
    public async Task Push(string type, string body, string link = "/Broker#activity")
    {
        try
        {
            var n = new AppNotification { Type = type, Body = body, Link = link };
            await _fs.AddNotification(n);

            await _hub.Clients.Group("broker").SendAsync("notify",
                new { id = n.Id, type = n.Type, body = n.Body, link = n.Link });

            var to = _c["Broker:Email"];
            if (!string.IsNullOrEmpty(to) && (type == "register" || type == "meeting" || type == "reminder"))
                await _mail.SendCustom(to, _t["n." + type], body, _t["mail.open"], BaseUrl() + link);
        }
        catch { /* الإشعار عمره ما يوقّف حاجة عند المستخدم */ }
    }

    // ---------- إشعارات الـ Business والـ Agency ----------
    public async Task PushUser(string userId, string type, string body, string link = "/Notifications")
    {
        if (string.IsNullOrEmpty(userId)) return;
        try
        {
            var n = new AppNotification { UserId = userId, Type = type, Body = body, Link = link };
            await _fs.AddUserNotification(n);
            await _userHub.Clients.Group("u:" + userId).SendAsync("notify",
                new { id = n.Id, type = n.Type, body = n.Body, link = n.Link });
        }
        catch { }
    }

    private async Task Mail(string? to, string subject, string body, string btn, string link, string lang, byte[]? ics = null)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        try { await _mail.SendLocalized(to, subject, body, btn, link, lang, ics); } catch { }
    }

    // ---------- قرار الـ Broker على الميعاد ----------
    public async Task MeetingDecision(Meeting m, string decision)   // approved | rejected | cancelled
    {
        var lang = string.IsNullOrEmpty(m.Lang) ? "ar" : m.Lang;
        var when = AppTime.ToLocal(m.StartsAt).ToString("dddd d MMMM yyyy, h:mm tt", Cult(lang));
        var agency = string.IsNullOrEmpty(m.AgencyName) ? _t.Get("meet.general", lang) : m.AgencyName;

        await PushUser(m.BusinessId, decision, $"{agency} - {when}", "/Meetings/Mine");

        if (!string.IsNullOrEmpty(m.AgencyId))
        {
            if (decision == "approved") await PushUser(m.AgencyId, "approved", $"{m.BusinessName} - {when}", "/Agency");
            if (decision == "cancelled") await PushUser(m.AgencyId, "cancelled", $"{m.BusinessName} - {when}", "/Agency");
        }

        var key = decision == "approved" ? "mail.meet.ok" : decision == "cancelled" ? "mail.meet.cancel" : "mail.meet.no";
        await Mail(m.BusinessEmail, _t.Get("mail.meet.subject", lang), $"{_t.Get(key, lang)} {when}",
            _t.Get("mail.open", lang), BaseUrl() + "/Meetings/Mine", lang,
            decision == "approved" ? Ics.ForMeeting(m, _t, lang) : null);
    }

    // ---------- التذكيرات (قبل 24 ساعة / قبل ساعة) ----------
    public async Task Reminder(Meeting m, string kind)   // day | hour
    {
        var lang = string.IsNullOrEmpty(m.Lang) ? "ar" : m.Lang;
        var when = AppTime.ToLocal(m.StartsAt).ToString("dddd d MMMM, h:mm tt", Cult(lang));
        var agency = string.IsNullOrEmpty(m.AgencyName) ? _t.Get("meet.general", lang) : m.AgencyName;
        var intro = _t.Get(kind == "hour" ? "rem.hour" : "rem.day", lang);
        var subject = _t.Get(kind == "hour" ? "rem.hour.subj" : "rem.day.subj", lang);
        var btn = _t.Get("mail.view", lang);
        var ics = Ics.ForMeeting(m, _t, lang);

        // الـ Business
        var body = $"{intro} {agency} - {when}";
        await PushUser(m.BusinessId, "reminder", body, "/Meetings/Mine");
        await Mail(m.BusinessEmail, subject, body, btn, BaseUrl() + "/Meetings/Mine", lang, ics);

        // الوكالة
        if (!string.IsNullOrEmpty(m.AgencyId))
        {
            var ag = await _fs.GetUserById(m.AgencyId);
            if (ag != null)
            {
                var abody = $"{intro} {m.BusinessName} - {when}";
                await PushUser(ag.Id, "reminder", abody, "/Agency");
                await Mail(ag.Email, subject, abody, btn, BaseUrl() + "/Agency", lang, ics);
            }
        }

        // الـ Broker
        await Push("reminder", $"{m.BusinessName} / {agency} / {when}", "/Broker#meetings");
    }
}