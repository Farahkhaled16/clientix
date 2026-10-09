using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using BrokerHub.Models;

namespace BrokerHub.Services;

public class GoogleCalendarService
{
    private const string Scope = "https://www.googleapis.com/auth/calendar.events";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string Events = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private static readonly ConcurrentDictionary<string, (string Token, DateTime Exp)> Cache = new();

    private readonly HttpClient _http;
    private readonly IConfiguration _c;
    private readonly FirestoreService _fs;
    private readonly IDataProtector _dp;
    private readonly Translator _t;

    public GoogleCalendarService(HttpClient http, IConfiguration c, FirestoreService fs,
                                 IDataProtectionProvider dp, Translator t)
    { _http = http; _c = c; _fs = fs; _dp = dp.CreateProtector("clientix.gcal"); _t = t; }

    // ---------- الربط ----------
    public string AuthUrl(string redirectUri, string state) =>
        "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new[]
        {
            $"client_id={Uri.EscapeDataString(_c["Google:ClientId"] ?? "")}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            $"scope={Uri.EscapeDataString(Scope)}",
            "access_type=offline",
            "prompt=consent",
            $"state={Uri.EscapeDataString(state)}"
        });

    private FormUrlEncodedContent Form(params (string k, string v)[] extra)
    {
        var d = new Dictionary<string, string>
        {
            ["client_id"] = _c["Google:ClientId"] ?? "",
            ["client_secret"] = _c["Google:ClientSecret"] ?? ""
        };
        foreach (var (k, v) in extra) d[k] = v;
        return new FormUrlEncodedContent(d);
    }

    public async Task<bool> Exchange(string code, string redirectUri, string userId)
    {
        var res = await _http.PostAsync(TokenUrl, Form(("code", code), ("redirect_uri", redirectUri),
                                                        ("grant_type", "authorization_code")));
        if (!res.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var scopes = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? "" : "";
        if (!scopes.Contains("calendar.events")) return false;          // المستخدم شال إذن التقويم
        if (!root.TryGetProperty("refresh_token", out var rt) || string.IsNullOrEmpty(rt.GetString())) return false;

        await _fs.SaveCalendarLink(new CalendarLink { UserId = userId, RefreshToken = _dp.Protect(rt.GetString()!) });
        Cache[userId] = (root.GetProperty("access_token").GetString()!,
                         DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 60));
        return true;
    }

    public async Task<bool> IsConnected(string userId) => await _fs.GetCalendarLink(userId) != null;

    public async Task Disconnect(string userId, string role)
    {
        try { foreach (var m in await Upcoming(userId, role)) await Remove(userId, m.Id); } catch { }

        var link = await _fs.GetCalendarLink(userId);
        if (link != null)
        {
            try
            {
                var rt = _dp.Unprotect(link.RefreshToken);
                await _http.PostAsync("https://oauth2.googleapis.com/revoke",
                    new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("token", rt) }));
            }
            catch { }
        }
        await _fs.DeleteCalendarLink(userId);
        Cache.TryRemove(userId, out _);
    }

    private async Task<string?> AccessToken(string userId)
    {
        if (Cache.TryGetValue(userId, out var hit) && hit.Exp > DateTime.UtcNow) return hit.Token;

        var link = await _fs.GetCalendarLink(userId);
        if (link == null) return null;

        string refresh;
        try { refresh = _dp.Unprotect(link.RefreshToken); }
        catch { await _fs.DeleteCalendarLink(userId); return null; }

        var res = await _http.PostAsync(TokenUrl, Form(("refresh_token", refresh), ("grant_type", "refresh_token")));
        if (!res.IsSuccessStatusCode)
        {
            if ((int)res.StatusCode is 400 or 401) await _fs.DeleteCalendarLink(userId);   // اتلغى الإذن أو انتهى
            return null;
        }

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("access_token").GetString()!;
        Cache[userId] = (token, DateTime.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 60));
        return token;
    }

    // ---------- الأحداث ----------
    private static string EventId(string meetingId) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("clientix:" + meetingId))).ToLowerInvariant();

    private static string Z(DateTime d) =>
        DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, object? body)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return _http.SendAsync(req);
    }

    private (string title, string desc) Describe(Meeting m, string role)
    {
        var lang = string.IsNullOrEmpty(m.Lang) ? "ar" : m.Lang;
        var agency = string.IsNullOrEmpty(m.AgencyName) ? _t.Get("meet.general", lang) : m.AgencyName;
        return role switch
        {
            "agency" => ($"{_t.Get("cal.with", lang)} {m.BusinessName}", agency),
            "broker" => ($"{m.BusinessName} - {agency}", m.Note ?? ""),
            _ => (_t.Get("meet.ics.title", lang), agency)
        };
    }

    private async Task Upsert(string userId, Meeting m, string role)
    {
        var token = await AccessToken(userId);
        if (token == null) return;

        var (title, desc) = Describe(m, role);
        var id = EventId(m.Id);
        var body = new Dictionary<string, object>
        {
            ["summary"] = title,
            ["description"] = desc,
            ["status"] = "confirmed",
            ["start"] = new { dateTime = Z(m.StartsAt), timeZone = "Africa/Cairo" },
            ["end"] = new { dateTime = Z(m.StartsAt.AddMinutes(30)), timeZone = "Africa/Cairo" },
            ["reminders"] = new
            {
                useDefault = false,
                overrides = new[] { new { method = "popup", minutes = 1440 }, new { method = "popup", minutes = 60 } }
            }
        };

        var res = await Send(HttpMethod.Put, $"{Events}/{id}?sendUpdates=none", token, body);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            body["id"] = id;
            await Send(HttpMethod.Post, $"{Events}?sendUpdates=none", token, body);
        }
    }

    private async Task Remove(string userId, string meetingId)
    {
        var token = await AccessToken(userId);
        if (token == null) return;
        await Send(HttpMethod.Delete, $"{Events}/{EventId(meetingId)}?sendUpdates=none", token, null);   // 404 أو 410 عادي
    }

    // بعد أي قرار من الـ Broker: قبول = إضافة/تحديث، غير كده = مسح
    public async Task SyncMeeting(Meeting m)
    {
        var parties = new List<(string id, string role)> { (m.BusinessId, "business"), ("broker", "broker") };
        if (!string.IsNullOrEmpty(m.AgencyId)) parties.Add((m.AgencyId, "agency"));

        foreach (var (id, role) in parties)
        {
            try
            {
                if (m.Status == "approved") await Upsert(id, m, role);
                else await Remove(id, m.Id);
            }
            catch { }
        }
    }

    private async Task<List<Meeting>> Upcoming(string userId, string role)
    {
        var from = DateTime.UtcNow.AddHours(-1);
        var all = (await _fs.GetMeetings()).Where(m => m.Status == "approved" && m.StartsAt > from);
        return (role switch
        {
            "broker" => all,
            "agency" => all.Where(m => m.AgencyId == userId),
            _ => all.Where(m => m.BusinessId == userId)
        }).ToList();
    }

    // أول ما يربط: نضيف كل مواعيده المؤكدة القادمة
    public async Task SyncAllFor(string userId, string role)
    {
        foreach (var m in await Upcoming(userId, role))
        {
            try { await Upsert(userId, m, role); } catch { }
        }
    }
}