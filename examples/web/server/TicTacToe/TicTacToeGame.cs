namespace HaaS.Host.Web.TicTacToe;

public class TicTacToeGame
{
    private readonly char[] _board = new char[9];
    private bool _aiHasMovedThisTurn;

    public TicTacToeGame() => Array.Fill(_board, ' ');

    public IReadOnlyList<char> Board => _board;

    public bool AiHasMovedThisTurn => _aiHasMovedThisTurn;

    public bool IsValidMove(int position)
    {
        return position >= 1 && position <= 9 && _board[position - 1] == ' ';
    }

    public IEnumerable<int> GetValidMoves()
    {
        var positions = new List<int>();
        for (var i = 0; i < _board.Length; i++)
            if (_board[i] == ' ')
                positions.Add(i + 1);
        return positions;
    }

    public bool TryPlaceAiMarker(int position)
    {
        if (_aiHasMovedThisTurn || !IsValidMove(position))
            return false;

        _board[position - 1] = 'O';
        _aiHasMovedThisTurn = true;
        return true;
    }

    public void PlacePlayerMarker(int position)
    {
        if (IsValidMove(position))
        {
            _board[position - 1] = 'X';
        }
    }

    public void ResetTurn() => _aiHasMovedThisTurn = false;

    public char? GetWinner()
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
                return _board[a];
            }
        }

        return null;
    }

    public bool IsDraw() => Array.TrueForAll(_board, c => c != ' ') && GetWinner() == null;
}
