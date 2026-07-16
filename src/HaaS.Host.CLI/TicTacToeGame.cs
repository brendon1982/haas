namespace HaaS.Host.CLI;

public class TicTacToeGame
{
    private readonly char[] _board = new char[9];
    private bool _hasMovedThisTurn;

    public TicTacToeGame() => Array.Fill(_board, ' ');

    public IReadOnlyList<char> Board => _board;

    public bool HasMovedThisTurn => _hasMovedThisTurn;

    public bool TryPlace(int position)
    {
        if (_hasMovedThisTurn)
            return false;

        if (position < 1 || position > 9 || _board[position - 1] != ' ')
            return false;

        _board[position - 1] = 'O';
        _hasMovedThisTurn = true;
        return true;
    }

    public void ResetTurn() => _hasMovedThisTurn = false;

    public void PlacePlayer(int position) => _board[position - 1] = 'X';

    public string PlaceMarker(int position)
    {
        if (HasMovedThisTurn)
            return "You have already placed your marker this turn. Wait for the next turn.";
        if (!TryPlace(position))
            return $"Position {position} is not available. Choose from: {FormatValidMoves()}.";
        return $"Placed O at position {position}. Your turn is over. Wait for the player to move before your next turn.";
    }

    public string FormatBoard()
    {
        return $"  {Cell(0, 0)} | {Cell(1, 0)} | {Cell(2, 0)}\n" +
               $"  ---+---+---\n" +
               $"  {Cell(0, 1)} | {Cell(1, 1)} | {Cell(2, 1)}\n" +
               $"  ---+---+---\n" +
               $"  {Cell(0, 2)} | {Cell(1, 2)} | {Cell(2, 2)}";
    }

    public string FormatValidMoves()
    {
        var positions = new List<int>();
        for (var i = 0; i < _board.Length; i++)
            if (_board[i] == ' ')
                positions.Add(i + 1);

        return string.Join(", ", positions);
    }

    public bool TryGetWinner(out char winner)
    {
        var lines = new[]
        {
            (0, 1, 2), (3, 4, 5), (6, 7, 8),
            (0, 3, 6), (1, 4, 7), (2, 5, 8),
            (0, 4, 8), (2, 4, 6)
        };

        foreach (var (a, b, c) in lines)
        {
            if (_board[a] != ' ' && _board[a] == _board[b] && _board[b] == _board[c])
            {
                winner = _board[a];
                return true;
            }
        }

        winner = default;
        return false;
    }

    public bool IsDraw() => Array.TrueForAll(_board, c => c != ' ');

    private char Cell(int col, int row) => _board[row * 3 + col];
}
