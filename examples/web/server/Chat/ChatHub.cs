using Microsoft.AspNetCore.SignalR;
using HaaS.Domain.ValueObjects;
using HaaS.Host.Web.Infrastructure;

namespace HaaS.Host.Web.Chat;

public class ChatHub : Hub
{
    private readonly WebSignalBus _bus;

    public ChatHub(WebSignalBus bus)
    {
        _bus = bus;
    }

    public async Task SendMessage(string message)
    {
        var messageId = Guid.NewGuid().ToString();
        await _bus.PushAsync("chat", new IncomingSignal(message, Context.ConnectionId, MessageId: messageId));
    }
}
