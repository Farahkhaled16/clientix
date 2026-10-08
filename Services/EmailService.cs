using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BrokerHub.Services;

public class EmailService
{
    private readonly IConfiguration _c;
    private readonly Translator _t;
    public EmailService(IConfiguration c, Translator t) { _c = c; _t = t; }

    public Task SendConfirm(string to, string link) =>
        Send(to, _t["mail.confirm.subject"], _t["mail.confirm.body"], _t["mail.confirm.btn"], link, null, null);

    public Task SendReset(string to, string link) =>
        Send(to, _t["mail.reset.subject"], _t["mail.reset.body"], _t["mail.reset.btn"], link, null, null);

    public Task SendCustom(string to, string subject, string body, string btn, string link) =>
        Send(to, subject, body, btn, link, null, null);

    public Task SendLocalized(string to, string subject, string body, string btn, string link, string lang, byte[]? ics = null) =>
        Send(to, subject, body, btn, link, lang, ics);

    private async Task Send(string to, string subject, string body, string btn, string link, string? lang, byte[]? ics)
    {
        lang ??= _t.Lang;
        var dir = lang == "ar" ? "rtl" : "ltr";
        var html = $@"
<div style='font-family:Segoe UI,Tahoma,sans-serif;direction:{dir};max-width:480px;margin:auto;padding:24px'>
  <h2 style='color:#2A5A4A'>{_t.Get("site.name", lang)}</h2>
  <p style='color:#26313A'>{body}</p>
  <p><a href='{link}' style='display:inline-block;background:#2A5A4A;color:#fff;padding:12px 28px;border-radius:999px;text-decoration:none'>{btn}</a></p>
</div>";

        var user = _c["Email:User"] ?? "";
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_c["Email:FromName"] ?? "Clientix", user));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = html };
        if (ics != null)
            builder.Attachments.Add("meeting.ics", ics, ContentType.Parse("text/calendar; charset=utf-8; method=PUBLISH"));
        msg.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_c["Email:Host"] ?? "smtp.gmail.com", int.Parse(_c["Email:Port"] ?? "587"), SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(user, _c["Email:Password"] ?? "");
        await smtp.SendAsync(msg);
        await smtp.DisconnectAsync(true);
    }
}