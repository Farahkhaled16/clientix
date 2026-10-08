using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Services;

namespace BrokerHub.Pages.Account;

public class ResetModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly AuthService _auth;
    public ResetModel(FirestoreService fs, AuthService auth) { _fs = fs; _auth = auth; }

    [BindProperty(SupportsGet = true)] public string Token { get; set; } = "";
    public string? Error { get; set; }
    public bool Done { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string password)
    {
        var user = await _fs.GetUserByField("ResetToken", Token);
        if (user == null || user.ResetExpires == null || user.ResetExpires < DateTime.UtcNow)
        { Error = "reset.bad"; return Page(); }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        { Error = "auth.weak"; return Page(); }

        user.PasswordHash = _auth.Hash(user, password);
        user.ResetToken = "";
        user.ResetExpires = null;
        user.EmailConfirmed = true;   // وصل للإيميل يبقى الإيميل بتاعه
        await _fs.UpdateUser(user);
        Done = true;
        return Page();
    }


}