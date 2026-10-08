using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Broker;

[RequestSizeLimit(104_857_600)]
[RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
public class EditModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly AuthService _auth;
    private readonly FileStorage _files;
    private readonly AutofillService _ai;

    public EditModel(FirestoreService fs, AuthService auth, FileStorage files, AutofillService ai)
    { _fs = fs; _auth = auth; _files = files; _ai = ai; }

    [BindProperty(SupportsGet = true)] public string? Id { get; set; }
    public bool IsNew => string.IsNullOrEmpty(Id);
    public Portfolio P { get; set; } = new();
    public string? Error { get; set; }
    public string? Note { get; set; }
    public string? Email { get; set; }
    public bool Saved { get; set; }

    private async Task<Portfolio> Load() =>
        IsNew ? new Portfolio() : (await _fs.GetPortfolio(Id!) ?? new Portfolio { AgencyId = Id! });

    public async Task OnGetAsync(bool saved = false)
    {
        Saved = saved;
        P = await Load();
    }

    // Autofill: يملا الفورم من غير حفظ
    public async Task<IActionResult> OnPostAutofillAsync(IFormFile? cv, string? email)
    {
        Email = email;
        P = await Load();

        if (cv == null || cv.Length == 0) { Error = "pf.nofile"; return Page(); }

        var x = await _ai.Extract(cv);
        if (x == null) { Error = "pf.autofail"; return Page(); }

        if (!string.IsNullOrWhiteSpace(x.CompanyName)) P.CompanyName = x.CompanyName;
        if (!string.IsNullOrWhiteSpace(x.About)) P.About = x.About;
        if (!string.IsNullOrWhiteSpace(x.Services)) P.Services = x.Services;
        if (!string.IsNullOrWhiteSpace(x.City)) P.City = x.City;
        if (x.FoundedYear > 0) P.FoundedYear = x.FoundedYear;
        if (x.TeamSize > 0) P.TeamSize = x.TeamSize;
        if (!string.IsNullOrWhiteSpace(x.Website)) P.Website = x.Website;
        if (!string.IsNullOrWhiteSpace(x.Instagram)) P.Instagram = x.Instagram;
        if (!string.IsNullOrWhiteSpace(x.LinkedIn)) P.LinkedIn = x.LinkedIn;

        Note = "pf.autofilled";
        return Page();
    }

    // حفظ البروفايل
    public async Task<IActionResult> OnPostAsync(Portfolio form, string? email, string? password, IFormFile? logoFile)
    {
        if (IsNew)
        {
            email = (email ?? "").Trim().ToLower();
            Email = email;
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            { Error = "auth.weak"; P = form; return Page(); }
            if (await _fs.GetUserByEmail(email) != null)
            { Error = "auth.exists"; P = form; return Page(); }

            var user = new AppUser
            {
                Name = form.CompanyName.Trim(),
                Email = email,
                Role = "agency",
                EmailConfirmed = true
            };
            user.PasswordHash = _auth.Hash(user, password);
            await _fs.CreateUser(user);
            Id = user.Id;
        }

        var p = await _fs.GetPortfolio(Id!) ?? new Portfolio();
        p.AgencyId = Id!;
        p.CompanyName = form.CompanyName.Trim();
        p.About = form.About ?? "";
        p.Services = form.Services ?? "";
        p.City = form.City ?? "";
        p.FoundedYear = form.FoundedYear;
        p.TeamSize = form.TeamSize;
        p.Website = form.Website ?? "";
        p.Instagram = form.Instagram ?? "";
        p.LinkedIn = form.LinkedIn ?? "";
        p.LogoUrl = form.LogoUrl ?? "";

        var logo = await _files.Save(logoFile);
        if (logo is { Kind: "image" }) p.LogoUrl = logo.Value.Url;

        await _fs.SavePortfolio(p);
        return RedirectToPage(new { id = Id, saved = true });
    }

    // إضافة عمل (صورة / فيديو / ملف / لينك)
    public async Task<IActionResult> OnPostAddWorkAsync(WorkItem work, IFormFile? media)
    {
        var up = await _files.Save(media);
        if (up != null) { work.MediaUrl = up.Value.Url; work.MediaType = up.Value.Kind; }

        var p = await _fs.GetPortfolio(Id!) ?? new Portfolio { AgencyId = Id! };
        p.Works.Add(work);
        await _fs.SavePortfolio(p);
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteWorkAsync(int index)
    {
        var p = await _fs.GetPortfolio(Id!);
        if (p != null && index >= 0 && index < p.Works.Count)
        {
            p.Works.RemoveAt(index);
            await _fs.SavePortfolio(p);
        }
        return RedirectToPage(new { id = Id });
    }
}