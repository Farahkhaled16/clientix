using System.Security.Claims;
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
builder.Services.AddScoped<EmailService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<NotifyService>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddHttpClient<AutofillService>();
builder.Services.AddHostedService<ReminderService>();
builder.Services.AddSingleton<CalendarLinks>();

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

// تغيير اللغة
app.MapGet("/setlang", (string lang, string? returnUrl, HttpContext ctx) =>
{
    ctx.Response.Cookies.Append("lang", lang == "en" ? "en" : "ar",
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")
        ? returnUrl : "/";
    return Results.LocalRedirect(target);
});

// تسجيل الخروج
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/");
}).DisableAntiforgery();

// ملف التقويم (.ics) للميعاد المؤكد
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

// للتجربة فقط (في Development): /dev/remind/{meetingId}?kind=day أو hour
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/remind/{id}", async (string id, string? kind, FirestoreService fs, NotifyService n) =>
    {
        var m = await fs.GetMeeting(id);
        if (m == null) return Results.NotFound();
        await n.Reminder(m, kind == "day" ? "day" : "hour");
        return Results.Ok("sent");
    });
}

// اشتراك التقويم: /cal/{token}/clientix.ics
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

// الدخول بجوجل
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

// SignalR hubs
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<UserHub>("/hubs/user");

app.MapRazorPages();
app.Run();