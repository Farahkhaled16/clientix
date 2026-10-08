using System.Security.Claims;
using System.Text.RegularExpressions;
using BrokerHub.Models;
using BrokerHub.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Translator>();

builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<NotifyService>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddHttpClient<AutofillService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddCookie("External")   // كوكي مؤقت أثناء الدخول بجوجل
    .AddGoogle(o =>
    {
        o.ClientId = builder.Configuration["Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        o.SignInScheme = "External";

        // يجبر جوجل يعرض شاشة اختيار الحساب (Choose an account / Use another account)
        o.Events.OnRedirectToAuthorizationEndpoint = ctx =>
        {
            ctx.Response.Redirect(ctx.RedirectUri + "&prompt=select_account");
            return Task.CompletedTask;
        };
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

app.MapRazorPages();
app.Run();