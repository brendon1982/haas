using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class WebSignalPresenter : ISignalPresenter
{
    protected readonly IHubContext<HaaSWebHub> HubContext;
    protected readonly string SourceType;

    public WebSignalPresenter(IHubContext<HaaSWebHub> hubContext, string sourceType)
    {
        HubContext = hubContext;
        SourceType = sourceType;
    }

    public virtual async Task PresentAsync(SessionResult result)
    {
        await HubContext.Clients.Client(result.SessionId!)
            .SendAsync("ReceiveMessage", SourceType, result.Output);
    }

    public async Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        if (sessionId != null)
        {
            await HubContext.Clients.Client(sessionId)
                .SendAsync("ReceiveError", SourceType, exception.Message);
        }
    }
}
