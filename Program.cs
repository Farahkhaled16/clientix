using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BrokerHub.Models;
using BrokerHub.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")))
    .SetApplicationName("clientix");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Translator>();

builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<CalendarLinks>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<NotifyService>();
builder.Services.AddHttpClient<GoogleCalendarService>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddHttpClient<AutofillService>();
builder.Services.AddHostedService<ReminderService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddCookie("External")
    .AddGoogle(o =>
    {
        o.ClientId = builder.Configuration["Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        o.SignInScheme = "External";
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// إشعار "زائر جديد" للـ Broker
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var isEntry = path == "/" || path.StartsWith("/Agencies", StringComparison.OrdinalIgnoreCase);

    if (ctx.Request.Method == "GET" && isEntry
        && ctx.User.Identity?.IsAuthenticated != true
        && !ctx.Request.Cookies.ContainsKey("vid"))
    {
        var ua = ctx.Request.Headers.UserAgent.ToString();
        if (ua.Length > 0 && !Regex.IsMatch(ua, "bot|crawl|spider|slurp|preview|monitor", RegexOptions.IgnoreCase))
        {
            ctx.Response.Cookies.Append("vid", Guid.NewGuid().ToString("N"), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddMinutes(30),
                HttpOnly = true,
                IsEssential = true
            });
            var device = ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "Mobile" : "Desktop";
            await ctx.RequestServices.GetRequiredService<NotifyService>().Push("visit", $"{device} / {path}");
        }
    }
    await next();
});

// ---------- لغة / خروج ----------
app.MapGet("/setlang", (string lang, string? returnUrl, HttpContext ctx) =>
{
    ctx.Response.Cookies.Append("lang", lang == "en" ? "en" : "ar",
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")
        ? returnUrl : "/";
    return Results.LocalRedirect(target);
});

app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/");
}).DisableAntiforgery();

// ---------- Service Worker (فيه إعدادات Firebase) ----------
app.MapGet("/sw.js", (HttpContext ctx, IConfiguration c) =>
{
    var cfg = JsonSerializer.Serialize(new
    {
        apiKey = c["FirebaseWeb:ApiKey"] ?? "",
        projectId = c["Firebase:ProjectId"] ?? "",
        messagingSenderId = c["FirebaseWeb:SenderId"] ?? "",
        appId = c["FirebaseWeb:AppId"] ?? ""
    });
    ctx.Response.Headers.CacheControl = "no-cache";
    return Results.Text(SwJs.Template.Replace("__CONFIG__", cfg), "application/javascript; charset=utf-8");
});

// ---------- الإشعارات (صندوق الجرس) ----------
app.MapGet("/notifications/list", async (HttpContext ctx, FirestoreService fs, Translator t) =>
{
    var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var broker = ctx.User.IsInRole("broker");
    var items = broker ? await fs.GetNotifications(15) : await fs.GetUserNotifications(uid, 15);
    var unread = broker ? await fs.CountUnread() : await fs.CountUserUnread(uid);

    return Results.Json(new
    {
        unread,
        items = items.Select(n => new
        {
            id = n.Id,
            type = n.Type,
            title = t["n." + n.Type],
            body = n.Body,
            link = n.Link,
            read = n.Read,
            at = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc)
        })
    });
}).RequireAuthorization();

app.MapPost("/notifications/read-all", async (HttpContext ctx, FirestoreService fs) =>
{
    if (ctx.User.IsInRole("broker")) await fs.MarkAllRead();
    else await fs.MarkUserRead(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

// ---------- Push tokens ----------
app.MapPost("/push/register", async (PushReg body, HttpContext ctx, FirestoreService fs) =>
{
    if (string.IsNullOrWhiteSpace(body.Token) || body.Token.Length > 4096) return Results.BadRequest();
    var ua = ctx.Request.Headers.UserAgent.ToString();
    await fs.SavePushToken(new PushToken
    {
        Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.Token))),
        UserId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
        Token = body.Token,
        Agent = ua.Length > 200 ? ua[..200] : ua,
        CreatedAt = DateTime.UtcNow
    });
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

app.MapPost("/push/unregister", async (PushReg body, FirestoreService fs) =>
{
    if (string.IsNullOrWhiteSpace(body.Token)) return Results.BadRequest();
    await fs.DeletePushToken(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.Token))));
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

// ---------- ملف التقويم (.ics) واشتراك التقويم ----------
app.MapGet("/meetings/ics/{id}", async (string id, HttpContext ctx, FirestoreService fs, Translator t) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true) return Results.Redirect("/Account/Login");

    var m = await fs.GetMeeting(id);
    if (m == null || m.Status != "approved") return Results.NotFound();

    var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var allowed = ctx.User.IsInRole("broker") || m.BusinessId == uid || m.AgencyId == uid;
    if (!allowed) return Results.NotFound();

    return Results.File(Ics.ForMeeting(m, t, t.Lang), "text/calendar", "clientix-meeting.ics");
});

app.MapGet("/cal/{token}/clientix.ics", async (string token, string? lang,
    FirestoreService fs, CalendarLinks links, Translator t) =>
{
    var parsed = links.Parse(token);
    if (parsed is null) return Results.NotFound();
    var (role, id) = parsed.Value;
    var l = lang == "en" ? "en" : "ar";

    var from = DateTime.UtcNow.AddDays(-30);
    var all = (await fs.GetMeetings()).Where(m => m.StartsAt > from);

    IEnumerable<Meeting> mine = role switch
    {
        "business" => all.Where(m => m.BusinessId == id && (m.Status == "approved" || m.Status == "pending")),
        "agency" => all.Where(m => m.AgencyId == id && m.Status == "approved"),
        "broker" => all.Where(m => m.Status == "approved" || m.Status == "pending"),
        _ => Enumerable.Empty<Meeting>()
    };

    var bytes = Ics.Feed(mine, t.Get("cal.name", l), m =>
    {
        var agency = string.IsNullOrEmpty(m.AgencyName) ? t.Get("meet.general", l) : m.AgencyName;
        var pending = m.Status == "pending";
        var suffix = pending ? " " + t.Get("cal.pending", l) : "";
        return role switch
        {
            "agency" => ($"{t.Get("cal.with", l)} {m.BusinessName}", agency, false),
            "broker" => ($"{m.BusinessName} - {agency}{suffix}", m.Note ?? "", pending),
            _ => (t.Get("meet.ics.title", l) + suffix, agency, pending)
        };
    }, t.Get("meet.ics.alarm", l));

    return Results.File(bytes, "text/calendar; charset=utf-8");
});

// ---------- ربط Google Calendar ----------
app.MapGet("/gcal/connect", (HttpContext ctx, GoogleCalendarService g) =>
{
    var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    ctx.Response.Cookies.Append("gcal_state", state, new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(10)
    });
    return Results.Redirect(g.AuthUrl($"{ctx.Request.Scheme}://{ctx.Request.Host}/gcal/callback", state));
}).RequireAuthorization();

app.MapGet("/gcal/callback", async (string? code, string? state, string? error, HttpContext ctx, GoogleCalendarService g) =>
{
    var expected = ctx.Request.Cookies["gcal_state"];
    ctx.Response.Cookies.Delete("gcal_state");

    if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code)
        || string.IsNullOrEmpty(expected) || expected != state)
        return Results.Redirect("/Calendar?gcal=denied");

    var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var role = ctx.User.IsInRole("broker") ? "broker" : ctx.User.IsInRole("agency") ? "agency" : "business";

    var ok = await g.Exchange(code, $"{ctx.Request.Scheme}://{ctx.Request.Host}/gcal/callback", uid);
    if (ok) await g.SyncAllFor(uid, role);
    return Results.Redirect(ok ? "/Calendar?gcal=connected" : "/Calendar?gcal=denied");
}).RequireAuthorization();

app.MapPost("/gcal/disconnect", async (HttpContext ctx, GoogleCalendarService g) =>
{
    var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var role = ctx.User.IsInRole("broker") ? "broker" : ctx.User.IsInRole("agency") ? "agency" : "business";
    await g.Disconnect(uid, role);
    return Results.Redirect("/Calendar?gcal=off");
}).RequireAuthorization().DisableAntiforgery();

// ---------- للتجربة فقط (Development) ----------
if (app.Environment.IsDevelopment())
{
    // /dev/remind/{meetingId}?kind=day|hour
    app.MapGet("/dev/remind/{id}", async (string id, string? kind, FirestoreService fs, NotifyService n) =>
    {
        var m = await fs.GetMeeting(id);
        if (m == null) return Results.NotFound();
        await n.Reminder(m, kind == "day" ? "day" : "hour");
        return Results.Ok("sent");
    });

    // بيبعت Push تجريبي ليك بعد 8 ثواني (غيّري التاب أو صغّري المتصفح عشان يظهر)
    app.MapGet("/dev/push", (HttpContext ctx, PushService p) =>
    {
        var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        _ = Task.Run(async () =>
        {
            await Task.Delay(8000);
            await p.SendTo(uid, "Clientix", "Test push 🔔", "/");
        });
        return Results.Text("Push will be sent in 8 seconds. Switch to another tab or minimize the browser.");
    }).RequireAuthorization();
}

// ---------- الدخول بجوجل ----------
app.MapGet("/google-login", (string? role, string? returnUrl) =>
{
    var props = new AuthenticationProperties
    {
        RedirectUri = $"/google-callback?role={Uri.EscapeDataString(role ?? "business")}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}"
    };
    return Results.Challenge(props, new[] { "Google" });
});

app.MapGet("/google-callback", async (string? role, string? returnUrl, HttpContext ctx, FirestoreService fs, NotifyService notify) =>
{
    var result = await ctx.AuthenticateAsync("External");
    if (!result.Succeeded) return Results.Redirect("/Account/Login");

    var email = result.Principal!.FindFirstValue(ClaimTypes.Email)!.Trim().ToLower();
    var name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

    var user = await fs.GetUserByEmail(email);
    if (user == null)
    {
        user = new AppUser { Name = name, Email = email, Role = "business", EmailConfirmed = true, Provider = "google" };
        await fs.CreateUser(user);
        await notify.Push("register", $"{user.Name} - {user.Email} (Google)");
    }
    else
    {
        if (!user.EmailConfirmed) { user.EmailConfirmed = true; await fs.UpdateUser(user); }
        await notify.Push("login", $"{user.Name} ({user.Role})");
    }

    await ctx.SignOutAsync("External");
    await AuthService.SignInAsync(ctx, user.Id, user.Name, user.Email, user.Role);
    return Results.LocalRedirect(AuthService.Target(returnUrl, user.Role));
});

// ---------- SignalR ----------
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<UserHub>("/hubs/user");

app.MapRazorPages();
app.Run();

public record PushReg(string? Token);