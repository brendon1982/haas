using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web.Infrastructure;

public class WebSignalPresenter<THub> : ISignalPresenter where THub : Hub
{
    protected readonly IHubContext<THub> HubContext;
    protected readonly string SourceType;

    public WebSignalPresenter(IHubContext<THub> hubContext, string sourceType)
    {
        HubContext = hubContext;
        SourceType = sourceType;
    }

    public virtual async Task PresentProcessingAsync(string sessionId, string? messageId = null)
    {
        await HubContext.Clients.Client(sessionId)
            .SendAsync("ProcessingStarted", SourceType, messageId);
    }

    public virtual async Task PresentAsync(SessionResult result)
    {
        await HubContext.Clients.Client(result.SessionId!)
            .SendAsync("ReceiveMessage", SourceType, result.Output, result.MessageId);
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
