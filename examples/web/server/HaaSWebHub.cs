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
    private readonly SessionManager _sessionManager;

    public HaaSWebHub(WebSignalBus bus, SessionManager sessionManager)
    {
        _bus = bus;
        _sessionManager = sessionManager;
    }

    public async Task SendMessage(string source, string message)
    {
        await _bus.PushAsync(source, new IncomingSignal(message, Context.ConnectionId));
    }

    public async Task SendMove(int position)
    {
        var game = _sessionManager.GetOrCreateTicTacToeGame(Context.ConnectionId);
        if (game.IsValidMove(position))
        {
            game.PlacePlayerMarker(position);
            game.ResetTurn();
            
            // Notify client about player move success
            await Clients.Caller.SendAsync("BoardUpdated", game.Board);
            
            // Trigger AI
            var message = $"The player (X) just moved at position {position}. It's your turn (O). Make your move.";
            await _bus.PushAsync("tictactoe", new IncomingSignal(message, Context.ConnectionId));
        }
    }

    public async Task ResetGame()
    {
        _sessionManager.ResetTicTacToeGame(Context.ConnectionId);
        var game = _sessionManager.GetOrCreateTicTacToeGame(Context.ConnectionId);
        await Clients.Caller.SendAsync("BoardUpdated", game.Board);
    }
}
