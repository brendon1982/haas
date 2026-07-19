using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class HaaSWebHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        // Potential logic to track sessions by ConnectionId
    }

    private readonly WebSignalBus _bus;

    public HaaSWebHub(WebSignalBus bus)
    {
        _bus = bus;
    }

    public async Task SendMessage(string source, string message)
    {
        await _bus.PushAsync(source, new IncomingSignal(message, Context.ConnectionId));
    }
}
