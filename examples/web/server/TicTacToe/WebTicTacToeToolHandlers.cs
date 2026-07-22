using System.Linq;
using Microsoft.AspNetCore.SignalR;
using HaaS.Host.Web.Infrastructure;

namespace HaaS.Host.Web.TicTacToe;

public class WebTicTacToeToolHandlers
{
    private readonly SessionManager _sessionManager;
    private readonly ScopedSessionContext _sessionContext;
    private readonly IHubContext<TicTacToeHub> _hubContext;

    public WebTicTacToeToolHandlers(SessionManager sessionManager, ScopedSessionContext sessionContext, IHubContext<TicTacToeHub> hubContext)
    {
        _sessionManager = sessionManager;
        _sessionContext = sessionContext;
        _hubContext = hubContext;
    }

    private TicTacToeGame GetGame() => _sessionManager.GetOrCreate<TicTacToeGame>(_sessionContext.SessionId ?? "unknown");

    public string GetBoard()
    {
        var game = GetGame();
        var b = game.Board;
        return $"  {b[0]} | {b[1]} | {b[2]}\n" +
               $"  ---+---+---\n" +
               $"  {b[3]} | {b[4]} | {b[5]}\n" +
               $"  ---+---+---\n" +
               $"  {b[6]} | {b[7]} | {b[8]}";
    }

    public string GetValidMoves() => string.Join(", ", GetGame().GetValidMoves());

    public async Task<string> PlaceMarker(int position)
    {
        var game = GetGame();
        if (game.AiHasMovedThisTurn)
            return "Error: You have already moved this turn. Wait for the player to move.";

        if (!game.IsValidMove(position))
            return $"Error: Position {position} is invalid or already taken. Valid moves: {string.Join(", ", game.GetValidMoves())}";

        game.TryPlaceAiMarker(position);
        
        // Notify client immediately
        if (_sessionContext.SessionId != null)
        {
            await _hubContext.Clients.Client(_sessionContext.SessionId)
                .SendAsync("BoardUpdated", game.Board.Select(c => c.ToString()).ToArray());
        }

        return $"Successfully placed 'O' at position {position}. Turn ended.";
    }
}
