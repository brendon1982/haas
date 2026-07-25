namespace HaaS.Host.Web.TicTacToe;

public sealed record TicTacToeState(
    string[] Board,
    string? Winner,
    bool IsDraw,
    bool IsGameOver)
{
    public static TicTacToeState FromGame(TicTacToeGame game)
    {
        var winner = game.GetWinner();
        var isDraw = game.IsDraw();

        return new TicTacToeState(
            game.Board.Select(marker => marker.ToString()).ToArray(),
            winner?.ToString(),
            isDraw,
            winner is not null || isDraw);
    }
}
