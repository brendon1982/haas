namespace HaaS.Host.CLI.TicTacToe;

public class TicTacToeToolHandlers
{
    private readonly TicTacToeGame _game;

    public TicTacToeToolHandlers(TicTacToeGame game)
    {
        _game = game;
    }

    public string GetBoard()
    {
        var b = _game.Board;
        return $"  {b[0]} | {b[1]} | {b[2]}\n" +
               $"  ---+---+---\n" +
               $"  {b[3]} | {b[4]} | {b[5]}\n" +
               $"  ---+---+---\n" +
               $"  {b[6]} | {b[7]} | {b[8]}";
    }

    public string GetValidMoves() => string.Join(", ", _game.GetValidMoves());

    public string PlaceMarker(int position)
    {
        if (_game.AiHasMovedThisTurn)
            return "Error: You have already moved this turn. Wait for the player to move.";

        if (!_game.IsValidMove(position))
            return $"Error: Position {position} is invalid or already taken. Valid moves: {string.Join(", ", _game.GetValidMoves())}";

        _game.TryPlaceAiMarker(position);
        return $"Successfully placed 'O' at position {position}. Turn ended.";
    }
}
