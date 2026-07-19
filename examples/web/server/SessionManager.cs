using System.Collections.Concurrent;
using HaaS.Host.Web.TicTacToe;

namespace HaaS.Host.Web;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, TicTacToeGame> _tictactoeGames = new();

    public TicTacToeGame GetOrCreateTicTacToeGame(string sessionId)
    {
        return _tictactoeGames.GetOrAdd(sessionId, _ => new TicTacToeGame());
    }

    public void ResetTicTacToeGame(string sessionId)
    {
        _tictactoeGames.TryRemove(sessionId, out _);
    }
}

public class ScopedSessionContext
{
    public string? SessionId { get; set; }
}
