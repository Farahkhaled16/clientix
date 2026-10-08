using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BrokerHub.Models;

namespace BrokerHub.Services;

[Authorize(Roles = "business,broker")]
public class ChatHub : Hub
{
    private readonly FirestoreService _fs;
    private readonly NotifyService _notify;
    public ChatHub(FirestoreService fs, NotifyService notify) { _fs = fs; _notify = notify; }

    private bool IsBroker => Context.User!.IsInRole("broker");
    private string UserId => Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public override async Task OnConnectedAsync()
    {
        if (IsBroker) await Groups.AddToGroupAsync(Context.ConnectionId, "broker-chat");
        else await Groups.AddToGroupAsync(Context.ConnectionId, "t:" + UserId);
        await base.OnConnectedAsync();
    }

    // الـ Broker يدخل غرفة المحادثة اللي فاتحها
    public Task JoinThread(string threadId) =>
        IsBroker ? Groups.AddToGroupAsync(Context.ConnectionId, "t:" + threadId) : Task.CompletedTask;

    public async Task Send(string? threadId, string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || text.Length > 2000) return;

        var tid = IsBroker ? threadId : UserId;     // الـ Business مايقدرش يكتب في محادثة غيره
        if (string.IsNullOrEmpty(tid)) return;

        var thread = await _fs.GetThread(tid);
        if (IsBroker && thread == null) return;     // الـ Broker يرد بس على محادثة موجودة
        var name = Context.User!.Identity?.Name ?? "";
        thread ??= new ChatThread { Id = tid, BusinessName = name };

        var firstUnread = !IsBroker && thread.UnreadForBroker == 0;

        var msg = new ChatMessage
        {
            ThreadId = tid,
            SenderRole = IsBroker ? "broker" : "business",
            SenderName = IsBroker ? "Broker" : name,
            Text = text
        };
        await _fs.AddMessage(msg);

        thread.LastText = text.Length > 80 ? text[..80] : text;
        thread.LastAt = msg.CreatedAt;
        if (IsBroker) thread.UnreadForBusiness++; else thread.UnreadForBroker++;
        await _fs.SaveThread(thread);

        await Clients.Group("t:" + tid).SendAsync("msg", new
        {
            id = msg.Id,
            threadId = tid,
            role = msg.SenderRole,
            name = msg.SenderName,
            text = msg.Text,
            at = msg.CreatedAt
        });

        await Clients.Group("broker-chat").SendAsync("threadUpdate", new
        {
            threadId = tid,
            name = thread.BusinessName,
            last = thread.LastText,
            role = msg.SenderRole
        });

        // إشعار للـ Broker عند أول رسالة غير مقروءة بس (عشان مايتزعجش)
        if (firstUnread)
            await _notify.Push("chat", $"{name}: {thread.LastText}", "/Broker/Chat?thread=" + tid);
    }

    public async Task MarkRead(string? threadId)
    {
        var tid = IsBroker ? threadId : UserId;
        if (string.IsNullOrEmpty(tid)) return;
        await _fs.MarkThreadRead(tid, IsBroker);
    }
}