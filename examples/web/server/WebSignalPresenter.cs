using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class WebSignalPresenter : ISignalPresenter
{
    private readonly IHubContext<HaaSWebHub> _hubContext;
    private readonly string _sourceType;
    private readonly SessionManager _sessionManager;

    public WebSignalPresenter(IHubContext<HaaSWebHub> hubContext, string sourceType, SessionManager sessionManager)
    {
        _hubContext = hubContext;
        _sourceType = sourceType;
        _sessionManager = sessionManager;
    }

    public async Task PresentAsync(SessionResult result)
    {
        await _hubContext.Clients.Client(result.SessionId)
            .SendAsync("ReceiveMessage", _sourceType, result.Output);

        if (_sourceType == "tictactoe")
        {
            var game = _sessionManager.GetOrCreateTicTacToeGame(result.SessionId);
            await _hubContext.Clients.Client(result.SessionId)
                .SendAsync("BoardUpdated", game.Board);
        }
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
    public ChatWebSignalPresenter(IHubContext<HaaSWebHub> hubContext, SessionManager sessionManager) 
        : base(hubContext, "chat", sessionManager) { }
}

public class TicTacToeWebSignalPresenter : WebSignalPresenter
{
    public TicTacToeWebSignalPresenter(IHubContext<HaaSWebHub> hubContext, SessionManager sessionManager) 
        : base(hubContext, "tictactoe", sessionManager) { }
}
