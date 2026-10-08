using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using BrokerHub.Models;

namespace BrokerHub.Services;

public class AuthService
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string Hash(AppUser u, string password) => _hasher.HashPassword(u, password);

    public bool Verify(AppUser u, string password) =>
        _hasher.VerifyHashedPassword(u, u.PasswordHash, password)
            != PasswordVerificationResult.Failed;

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static async Task SignInAsync(HttpContext ctx, string id, string name, string email, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(new ClaimsPrincipal(identity));
    }

    // بعد الدخول: يرجع للصفحة اللي كان فيها، أو للداشبورد
    public static string Target(string? returnUrl, string role)
    {
        if (!string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//"))
            return returnUrl;
        return role switch { "agency" => "/Agency", "broker" => "/Broker", _ => "/Business" };
    }
}