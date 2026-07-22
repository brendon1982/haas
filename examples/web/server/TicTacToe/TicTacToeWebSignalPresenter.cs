using System.Linq;
using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web.TicTacToe;

public class TicTacToeWebSignalPresenter : WebSignalPresenter
{
    private readonly SessionManager _sessionManager;

    public TicTacToeWebSignalPresenter(IHubContext<HaaSWebHub> hubContext, SessionManager sessionManager) 
        : base(hubContext, "tictactoe")
    {
        _sessionManager = sessionManager;
    }

    public override async Task PresentAsync(SessionResult result)
    {
        await base.PresentAsync(result);
        
        var game = _sessionManager.GetOrCreate<TicTacToeGame>(result.SessionId!);
        await HubContext.Clients.Client(result.SessionId!)
            .SendAsync("BoardUpdated", game.Board.Select(c => c.ToString()).ToArray());
    }
}
