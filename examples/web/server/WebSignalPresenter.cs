using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class WebSignalPresenter : ISignalPresenter
{
    private readonly IHubContext<HaaSWebHub> _hubContext;
    private readonly string _sourceType;

    public WebSignalPresenter(IHubContext<HaaSWebHub> hubContext, string sourceType)
    {
        _hubContext = hubContext;
        _sourceType = sourceType;
    }

    public async Task PresentAsync(SessionResult result)
    {
        await _hubContext.Clients.Client(result.SessionId)
            .SendAsync("ReceiveMessage", _sourceType, result.Output);
    }

    public async Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        if (sessionId != null)
        {
            await _hubContext.Clients.Client(sessionId)
                .SendAsync("ReceiveError", _sourceType, exception.Message);
        }
    }
}

public class ChatWebSignalPresenter : WebSignalPresenter
{
    public ChatWebSignalPresenter(IHubContext<HaaSWebHub> hubContext) : base(hubContext, "chat") { }
}

public class TicTacToeWebSignalPresenter : WebSignalPresenter
{
    public TicTacToeWebSignalPresenter(IHubContext<HaaSWebHub> hubContext) : base(hubContext, "tictactoe") { }
}
