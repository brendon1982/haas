using HaaS.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web.TicTacToe;

public class TicTacToeHubHandlers
{
    private readonly SessionManager _sessionManager;
    private readonly WebSignalBus _bus;

    public TicTacToeHubHandlers(SessionManager sessionManager, WebSignalBus bus)
    {
        _sessionManager = sessionManager;
        _bus = bus;
    }

    public async Task SendMove(Hub hub, int position)
    {
        var game = _sessionManager.GetOrCreate<TicTacToeGame>(hub.Context.ConnectionId);
        if (game.IsValidMove(position))
        {
            game.PlacePlayerMarker(position);
            game.ResetTurn();
            
            await hub.Clients.Caller.SendAsync("BoardUpdated", game.Board);
            
            // Trigger AI
            var message = $"The player (X) just moved at position {position}. It's your turn (O). Make your move.";
            await _bus.PushAsync("tictactoe", new IncomingSignal(message, hub.Context.ConnectionId));
        }
    }

    public async Task ResetGame(Hub hub)
    {
        _sessionManager.Remove<TicTacToeGame>(hub.Context.ConnectionId);
        var game = _sessionManager.GetOrCreate<TicTacToeGame>(hub.Context.ConnectionId);
        await hub.Clients.Caller.SendAsync("BoardUpdated", game.Board);
    }
}
