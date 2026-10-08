using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Services;

namespace BrokerHub.Pages.Account;

public class LoginModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly AuthService _auth;
    private readonly EmailService _mail;
    private readonly IConfiguration _config;
    private readonly NotifyService _notify;

    public LoginModel(FirestoreService fs, AuthService auth, EmailService mail, IConfiguration config, NotifyService notify)
    { _fs = fs; _auth = auth; _mail = mail; _config = config; _notify = notify; }

    [BindProperty(SupportsGet = true)] public string Role { get; set; } = "business";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
    public string? Info { get; set; }
    public bool ShowResend { get; set; }
    public string? PendingEmail { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var r = User.IsInRole("broker") ? "broker" : User.IsInRole("agency") ? "agency" : "business";
            return LocalRedirect(AuthService.Target(ReturnUrl, r));
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string email, string password)
    {
        email = (email ?? "").Trim().ToLower();

        // الـ Broker
        if (email == (_config["Broker:Username"] ?? "").ToLower() && password == _config["Broker:Password"])
        {
            await AuthService.SignInAsync(HttpContext, "broker", "Broker", "broker", "broker");
            return Redirect("/Broker");
        }

        var user = await _fs.GetUserByEmail(email);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !_auth.Verify(user, password))
        {
            Error = "auth.invalid";
            return Page();
        }

        if (!user.EmailConfirmed)
        {
            Error = "auth.unconfirmed";
            ShowResend = true;
            PendingEmail = email;
            return Page();
        }

        await AuthService.SignInAsync(HttpContext, user.Id, user.Name, user.Email, user.Role);
        await _notify.Push("login", $"{user.Name} ({user.Role})");
        return LocalRedirect(AuthService.Target(ReturnUrl, user.Role));
    }

    public async Task<IActionResult> OnPostResendAsync(string email)
    {
        email = (email ?? "").Trim().ToLower();
        var user = await _fs.GetUserByEmail(email);
        if (user != null && !user.EmailConfirmed)
        {
            user.ConfirmToken = AuthService.NewToken();
            await _fs.UpdateUser(user);
            try
            {
                await _mail.SendConfirm(user.Email,
                    $"{Request.Scheme}://{Request.Host}/Account/Confirm?token={user.ConfirmToken}");
            }
            catch { Error = "auth.mailfail"; return Page(); }
        }
        Info = "auth.sent";
        return Page();
    }
}