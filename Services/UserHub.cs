using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrokerHub.Services;

[Authorize(Roles = "business,agency")]
public class UserHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var uid = Context.User!.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(uid))
            await Groups.AddToGroupAsync(Context.ConnectionId, "u:" + uid);
        await base.OnConnectedAsync();
    }
}