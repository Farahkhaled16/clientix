using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BrokerHub.Models;
using BrokerHub.Services;

namespace BrokerHub.Pages.Broker;

public class ChatModel : PageModel
{
    private readonly FirestoreService _fs;
    public ChatModel(FirestoreService fs) => _fs = fs;

    [BindProperty(Name = "thread", SupportsGet = true)] public string? ThreadId { get; set; }
    public List<ChatThread> Threads { get; set; } = new();
    public ChatThread? Current { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();

    public async Task OnGetAsync()
    {
        Threads = await _fs.GetThreads();
        if (string.IsNullOrEmpty(ThreadId)) return;

        Current = Threads.FirstOrDefault(t => t.Id == ThreadId) ?? await _fs.GetThread(ThreadId);
        if (Current == null) return;

        Messages = await _fs.GetMessages(ThreadId);
        await _fs.MarkThreadRead(ThreadId, true);
        Current.UnreadForBroker = 0;
    }
}