using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Broker;

public class ChatModel : PageModel
{
    private readonly FirestoreService _fs;
    private readonly ChatService _chat;
    public ChatModel(FirestoreService fs, ChatService chat) { _fs = fs; _chat = chat; }

    [BindProperty(Name = "thread", SupportsGet = true)] public string? ThreadId { get; set; }
    public List<ChatThread> Threads { get; set; } = new();
    public ChatThread? Current { get; set; }
    public string InitJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        Threads = await _fs.GetThreads();
        if (string.IsNullOrEmpty(ThreadId)) return;

        Current = Threads.FirstOrDefault(t => t.Id == ThreadId) ?? await _fs.GetThread(ThreadId);
        if (Current == null) return;

        var items = await _chat.History(ThreadId, "broker", Current);
        InitJson = JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await _fs.MarkThreadSeen(ThreadId, true);
        Current.UnreadForBroker = 0;
    }
}