using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Services;

namespace BrokerHub.Pages.Account;

public class ForgotModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly EmailService _mail;
    public ForgotModel(FirestoreService fs, EmailService mail) { _fs = fs; _mail = mail; }
    public bool Sent { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string email)
    {
        var user = await _fs.GetUserByEmail((email ?? "").Trim().ToLower());
        if (user != null)
        {
            user.ResetToken = AuthService.NewToken();
            user.ResetExpires = DateTime.UtcNow.AddHours(1);
            await _fs.UpdateUser(user);
            try
            {
                await _mail.SendReset(user.Email,
                    $"{Request.Scheme}://{Request.Host}/Account/Reset?token={user.ResetToken}");
            }
            catch { /* منعرضش الخطأ عشان مانكشفش إذا الإيميل مسجل ولا لأ */ }
        }
        Sent = true;
        return Page();
    }
}