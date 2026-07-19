using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class HaaSWebHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        // Potential logic to track sessions by ConnectionId
    }

    public async Task SendMessage(string source, string message)
    {
        // This will be expanded in Step 2 to route to WebSignalSource
        await Clients.Caller.SendAsync("ReceiveMessage", "System", $"Enqueued message for {source}: {message}");
    }
}
