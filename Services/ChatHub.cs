using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BrokerHub.Models;

namespace BrokerHub.Services;

[Authorize(Roles = "business,broker")]
public class ChatHub : Hub
{
    private readonly FirestoreService _fs;
    private readonly ChatService _chat;

    public ChatHub(FirestoreService fs, ChatService chat) { _fs = fs; _chat = chat; }

    private bool IsBroker => Context.User!.IsInRole("broker");
    private string UserId => Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Role => IsBroker ? "broker" : "business";

    public override async Task OnConnectedAsync()
    {
        if (IsBroker) await Groups.AddToGroupAsync(Context.ConnectionId, "broker-chat");
        else await Groups.AddToGroupAsync(Context.ConnectionId, "t:" + UserId);
        await base.OnConnectedAsync();
    }

    public Task JoinThread(string threadId) =>
        IsBroker ? Groups.AddToGroupAsync(Context.ConnectionId, "t:" + threadId) : Task.CompletedTask;

    public async Task Send(string? threadId, string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || text.Length > 2000) return;

        var tid = IsBroker ? threadId : UserId;
        if (string.IsNullOrEmpty(tid)) return;

        await _chat.Post(tid, IsBroker, Context.User!.Identity?.Name ?? "",
            new ChatMessage { Kind = "text", Text = text });
    }

    // الـ Broker يبعت كارت وكالة
    public async Task SendAgency(string? threadId, string agencyId)
    {
        if (!IsBroker || string.IsNullOrEmpty(threadId)) return;
        var pf = await _fs.GetPortfolio(agencyId);
        if (pf == null || string.IsNullOrWhiteSpace(pf.CompanyName)) return;

        await _chat.Post(threadId, true, "Broker",
            new ChatMessage { Kind = "agency", AgencyId = agencyId, Text = pf.CompanyName });
    }

    public Task Typing(string? threadId)
    {
        var tid = IsBroker ? threadId : UserId;
        if (string.IsNullOrEmpty(tid)) return Task.CompletedTask;
        return Clients.OthersInGroup("t:" + tid).SendAsync("typing", new { threadId = tid, role = Role });
    }

    public async Task MarkRead(string? threadId)
    {
        var tid = IsBroker ? threadId : UserId;
        if (string.IsNullOrEmpty(tid)) return;

        await _fs.MarkThreadSeen(tid, IsBroker);
        await Clients.Group("t:" + tid).SendAsync("read", new { threadId = tid, by = Role });
    }
}