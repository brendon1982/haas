namespace HaaS.Host.CLI;

public class TicTacToeModule : ICliModule
{
    public string Name => "Tic-Tac-Toe";
    public string Description => "Classic 3-in-a-row game against a simple AI";

    public Task RunAsync(CancellationToken ct = default)
    {
        var board = new char[9];
        Array.Fill(board, ' ');

        while (true)
        {
            Console.Clear();
            DrawBoard(board);

            if (TryGetWinner(board, out var winner))
            {
                Console.WriteLine(winner == 'X' ? "You win!" : winner == 'O' ? "AI wins!" : "");
                break;
            }

            if (IsDraw(board))
            {
                Console.WriteLine("It's a draw!");
                break;
            }

            Console.WriteLine();
            Console.Write("Your move (1-9, or 0 to quit): ");
            var input = Console.ReadLine();

            if (input == "0" || string.IsNullOrWhiteSpace(input))
                break;

            if (!int.TryParse(input, out var pos) || pos < 1 || pos > 9 || board[pos - 1] != ' ')
            {
                Console.WriteLine("Invalid move. Press any key to try again...");
                Console.ReadKey(true);
                continue;
            }

            board[pos - 1] = 'X';
            ct.ThrowIfCancellationRequested();

            if (TryGetWinner(board, out _) || IsDraw(board))
                continue;

            var aiMove = GetAiMove(board);
            board[aiMove] = 'O';
        }

        Console.WriteLine();
        Console.WriteLine("Game over. Press any key to return to menu...");
        Console.ReadKey(true);
        return Task.CompletedTask;
    }

    private static void DrawBoard(char[] board)
    {
        Console.WriteLine("Tic-Tac-Toe");
        Console.WriteLine(new string('=', 20));
        Console.WriteLine();

        for (var row = 0; row < 3; row++)
        {
            Console.WriteLine($"  {Cell(board, 0, row)} | {Cell(board, 1, row)} | {Cell(board, 2, row)}");
            if (row < 2)
                Console.WriteLine("  ---+---+---");
        }
    }

    private static char Cell(char[] board, int col, int row) => board[row * 3 + col];

    private static bool TryGetWinner(char[] board, out char winner)
    {
        var lines = new[]
        {
            (0, 1, 2), (3, 4, 5), (6, 7, 8),
            (0, 3, 6), (1, 4, 7), (2, 5, 8),
            (0, 4, 8), (2, 4, 6)
        };

        foreach (var (a, b, c) in lines)
        {
            if (board[a] != ' ' && board[a] == board[b] && board[b] == board[c])
            {
                winner = board[a];
                return true;
            }
        }

        winner = default;
        return false;
    }

    private static bool IsDraw(char[] board) => Array.TrueForAll(board, c => c != ' ');

    private static int GetAiMove(char[] board)
    {
        var empty = new List<int>();
        for (var i = 0; i < board.Length; i++)
            if (board[i] == ' ')
                empty.Add(i);

        return empty[Random.Shared.Next(empty.Count)];
    }
}
