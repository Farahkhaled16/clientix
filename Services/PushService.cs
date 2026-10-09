using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace BrokerHub.Services;

public class PushService
{
    private readonly FirestoreService _fs;
    private readonly ILogger<PushService> _log;
    private readonly bool _ready;

    public PushService(FirestoreService fs, IConfiguration c, ILogger<PushService> log)
    {
        _fs = fs; _log = log;
        try
        {
            if (FirebaseApp.DefaultInstance == null)
                FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(c["Firebase:KeyPath"]!) });
            _ready = true;
        }
        catch (Exception e) { _log.LogError(e, "FCM init failed"); }
    }

    public async Task SendTo(string userId, string title, string body, string link)
    {
        if (!_ready || string.IsNullOrEmpty(userId)) return;
        try
        {
            var tokens = await _fs.GetPushTokens(userId);
            if (tokens.Count == 0) return;

            var text = body.Length > 160 ? body[..160] + "…" : body;
            var messages = tokens.Select(t => new Message
            {
                Token = t.Token,
                Data = new Dictionary<string, string> { ["title"] = title, ["body"] = text, ["link"] = link },
                Webpush = new WebpushConfig
                {
                    Headers = new Dictionary<string, string> { ["Urgency"] = "high", ["TTL"] = "86400" }
                }
            }).ToList();

            var res = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages);
            for (var i = 0; i < res.Responses.Count; i++)
            {
                var r = res.Responses[i];
                if (!r.IsSuccess && r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    await _fs.DeletePushToken(tokens[i].Id);   // الجهاز مسح الإذن أو التوكن انتهى
            }
        }
        catch (Exception e) { _log.LogWarning(e, "Push failed"); }
    }
}