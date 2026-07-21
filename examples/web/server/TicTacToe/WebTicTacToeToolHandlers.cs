namespace HaaS.Host.Web.TicTacToe;

public class WebTicTacToeToolHandlers
{
    private readonly SessionManager _sessionManager;
    private readonly ScopedSessionContext _sessionContext;

    public WebTicTacToeToolHandlers(SessionManager sessionManager, ScopedSessionContext sessionContext)
    {
        _sessionManager = sessionManager;
        _sessionContext = sessionContext;
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

    public string PlaceMarker(int position)
    {
        var game = GetGame();
        if (game.AiHasMovedThisTurn)
            return "Error: You have already moved this turn. Wait for the player to move.";

        if (!game.IsValidMove(position))
            return $"Error: Position {position} is invalid or already taken. Valid moves: {string.Join(", ", game.GetValidMoves())}";

        game.TryPlaceAiMarker(position);
        return $"Successfully placed 'O' at position {position}. Turn ended.";
    }
}
