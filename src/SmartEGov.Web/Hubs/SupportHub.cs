using Microsoft.AspNetCore.SignalR;

namespace SmartEGov.Web.Hubs;

public class SupportHub : Hub
{
    public async Task JoinGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }
}