using Microsoft.AspNetCore.SignalR;
using BrokerHub.Models;

namespace BrokerHub.Services;

public record AgencyDto(string Id, string Name, string City, string Logo, string Services);

public record ChatDto(string Id, string ThreadId, string Role, string Kind, string Text, string MediaUrl,
                      string FileName, DateTime At, bool Read, AgencyDto? Agency, string Preview);

public class ChatService
{
    private readonly FirestoreService _fs;
    private readonly IHubContext<ChatHub> _hub;
    private readonly NotifyService _notify;
    private readonly PushService _push;
    private readonly Translator _t;

    public ChatService(FirestoreService fs, IHubContext<ChatHub> hub, NotifyService notify, PushService push, Translator t)
    { _fs = fs; _hub = hub; _notify = notify; _push = push; _t = t; }

    public static string Preview(ChatMessage m, Translator t) => m.Kind switch
    {
        "image" => "📷 " + t["chat.prev.image"],
        "video" => "🎬 " + t["chat.prev.video"],
        "file" => "📎 " + (string.IsNullOrEmpty(m.FileName) ? t["chat.prev.file"] : m.FileName),
        "agency" => "🏢 " + t["chat.prev.agency"],
        _ => m.Text
    };

    public async Task<ChatDto> ToDto(ChatMessage m, bool read, Dictionary<string, Portfolio>? pf = null)
    {
        AgencyDto? ag = null;
        if (m.Kind == "agency" && !string.IsNullOrEmpty(m.AgencyId))
        {
            Portfolio? p = null;
            if (pf != null) pf.TryGetValue(m.AgencyId, out p);
            p ??= await _fs.GetPortfolio(m.AgencyId);
            if (p != null) ag = new AgencyDto(m.AgencyId, p.CompanyName, p.City ?? "", p.LogoUrl ?? "", p.Services ?? "");
        }
        return new ChatDto(m.Id, m.ThreadId, m.SenderRole, string.IsNullOrEmpty(m.Kind) ? "text" : m.Kind,
            m.Text ?? "", m.MediaUrl ?? "", m.FileName ?? "",
            DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc), read, ag, Preview(m, _t));
    }

    // سجل المحادثة بحالة القراءة (✓✓) محسوبة من وجهة نظر اللي فاتح الصفحة
    public async Task<List<ChatDto>> History(string threadId, string viewerRole, ChatThread? th)
    {
        var msgs = await _fs.GetMessages(threadId);
        Dictionary<string, Portfolio>? pf = null;
        if (msgs.Any(x => x.Kind == "agency"))
            pf = (await _fs.GetAllPortfolios()).ToDictionary(p => p.AgencyId);

        var counterpartRead = viewerRole == "broker" ? th?.BusinessReadAt : th?.BrokerReadAt;
        var list = new List<ChatDto>();
        foreach (var m in msgs)
        {
            var read = m.SenderRole == viewerRole && counterpartRead != null && counterpartRead >= m.CreatedAt;
            list.Add(await ToDto(m, read, pf));
        }
        return list;
    }

    // إرسال رسالة (نص / ملف / كارت وكالة) من الطرفين
    public async Task<ChatDto?> Post(string threadId, bool fromBroker, string senderName, ChatMessage m)
    {
        var thread = await _fs.GetThread(threadId);
        if (fromBroker && thread == null) return null;   // الـ Broker يرد بس على محادثة موجودة
        thread ??= new ChatThread { Id = threadId, BusinessName = senderName };

        var firstUnread = !fromBroker && thread.UnreadForBroker == 0;

        m.ThreadId = threadId;
        m.SenderRole = fromBroker ? "broker" : "business";
        m.SenderName = fromBroker ? "Broker" : senderName;
        await _fs.AddMessage(m);

        var preview = Preview(m, _t);
        thread.LastText = preview.Length > 80 ? preview[..80] : preview;
        thread.LastAt = m.CreatedAt;
        if (fromBroker) thread.UnreadForBusiness++; else thread.UnreadForBroker++;
        await _fs.SaveThread(thread);

        var dto = await ToDto(m, false);
        await _hub.Clients.Group("t:" + threadId).SendAsync("msg", dto);
        await _hub.Clients.Group("broker-chat").SendAsync("threadUpdate", new
        {
            threadId,
            name = thread.BusinessName,
            last = thread.LastText,
            role = m.SenderRole
        });

        if (firstUnread)
            await _notify.Push("chat", $"{senderName}: {thread.LastText}", "/Broker/Chat?thread=" + threadId);
        if (fromBroker)
            await _push.SendTo(threadId, _t["chat.newmsg"], thread.LastText, "/Chat");

        return dto;
    }
}