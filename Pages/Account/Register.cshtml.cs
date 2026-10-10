using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly AuthService _auth;
    private readonly EmailService _mail;
    private readonly NotifyService _notify;

    public RegisterModel(FirestoreService fs, AuthService auth, EmailService mail, NotifyService notify)
    { _fs = fs; _auth = auth; _mail = mail; _notify = notify; }

    [BindProperty(SupportsGet = true)] public string Role { get; set; } = "business";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
    public bool Done { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var r = User.IsInRole("broker") ? "broker" : "business";
            return LocalRedirect(AuthService.Target(ReturnUrl, r));
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string name, string email, string password, bool accept)
    {
        if (!accept) { Error = "auth.mustaccept"; return Page(); }

        email = (email ?? "").Trim().ToLower();

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        { Error = "auth.weak"; return Page(); }

        if (await _fs.GetUserByEmail(email) != null)
        { Error = "auth.exists"; return Page(); }

        var user = new AppUser
        {
            Name = (name ?? "").Trim(),
            Email = email,
            Role = "business",
            EmailConfirmed = false,
            ConfirmToken = AuthService.NewToken(),
            AcceptedTermsAt = DateTime.UtcNow
        };
        user.PasswordHash = _auth.Hash(user, password);
        await _fs.CreateUser(user);

        await _notify.Push("register", $"{user.Name} - {user.Email}");

        try
        {
            await _mail.SendConfirm(user.Email,
                $"{Request.Scheme}://{Request.Host}/Account/Confirm?token={user.ConfirmToken}");
        }
        catch (Exception ex)
        {
            var link = $"{Request.Scheme}://{Request.Host}/Account/Confirm?token={user.ConfirmToken}";
            Console.WriteLine("CONFIRM LINK: " + link);
            Error = "MAIL: " + ex.Message;
            return Page();
        }


        Done = true;
        return Page();
    }
}