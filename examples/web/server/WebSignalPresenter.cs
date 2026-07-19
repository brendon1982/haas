using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class WebSignalPresenter : ISignalPresenter
{
    private readonly IHubContext<HaaSWebHub> _hubContext;

    public WebSignalPresenter(IHubContext<HaaSWebHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PresentAsync(SessionResult result)
    {
        // Pushing the result back to the specific client
        // ConnectionId was stored as SessionId in the IncomingSignal
        await _hubContext.Clients.Client(result.SessionId)
            .SendAsync("ReceiveMessage", result.Output);
    }

    public async Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        if (sessionId != null)
        {
            await _hubContext.Clients.Client(sessionId)
                .SendAsync("ReceiveError", exception.Message);
        }
    }
}
