using System.Security.Cryptography;
using System.Text;

namespace BrokerHub.Services;

public class CalendarLinks
{
    private readonly byte[] _key;

    public CalendarLinks(IConfiguration c) =>
        _key = Encoding.UTF8.GetBytes((c["Calendar:Secret"] ?? c["Broker:Password"] ?? "clientix") + "|cal");

    private string Sig(string role, string id) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{role}:{id}")))[..32].ToLowerInvariant();

    public string Token(string role, string id) => $"{role}-{id}-{Sig(role, id)}";

    public (string role, string id)? Parse(string token)
    {
        var p = (token ?? "").Split('-');
        if (p.Length != 3) return null;
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sig(p[0], p[1])), Encoding.UTF8.GetBytes(p[2].ToLowerInvariant()));
        return ok ? (p[0], p[1]) : null;
    }
}