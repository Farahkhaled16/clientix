using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Services;

namespace BrokerHub.Pages.Account;

public class ConfirmModel : PageModel
{
    private readonly FirestoreService _fs;
    public ConfirmModel(FirestoreService fs) => _fs = fs;
    public bool Ok { get; set; }

    public async Task OnGetAsync(string? token)
    {
        var user = await _fs.GetUserByField("ConfirmToken", token ?? "");
        if (user == null) return;
        user.EmailConfirmed = true;
        user.ConfirmToken = "";
        await _fs.UpdateUser(user);
        Ok = true;
    }
}