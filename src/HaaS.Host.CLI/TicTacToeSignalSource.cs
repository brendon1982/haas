using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.CLI;

public class TicTacToeSignalSource : ISignalSource
{
    private readonly TicTacToeGame _game;

    public TicTacToeSignalSource(TicTacToeGame game)
    {
        _game = game;
    }

    public string Type => "tictactoe";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        while (true)
        {
            Console.Clear();
            DrawBoard();

            if (_game.TryGetWinner(out var winner))
            {
                Console.WriteLine(winner == 'X' ? "You win!" : "AI wins!");
                break;
            }

            if (_game.IsDraw())
            {
                Console.WriteLine("It's a draw!");
                break;
            }

            Console.Write("Your move (1-9, or 0 to quit): ");
            var input = Console.ReadLine();

            if (input == "0" || string.IsNullOrWhiteSpace(input))
                break;

            if (!int.TryParse(input, out var pos) || pos < 1 || pos > 9 || _game.Board[pos - 1] != ' ')
            {
                Console.WriteLine("Invalid move. Press any key to try again...");
                Console.ReadKey(true);
                continue;
            }

            _game.PlacePlayer(pos);

            if (_game.TryGetWinner(out _) || _game.IsDraw())
                continue;

            _game.ResetTurn();

            var signal = new IncomingSignal(
                $"The player (X) just moved at position {pos}. It's your turn (O). Make your move.");

            var boardBefore = _game.Board.ToArray();

            Console.WriteLine();
            Console.Write("AI is thinking");
            
            using var cts = new CancellationTokenSource();
            var thinkingTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    Console.Write(".");
                    await Task.Delay(500, cts.Token).ContinueWith(_ => { });
                }
            });

            try
            {
                var handle = await handler(signal);
                await handle.WaitForResultAsync(cts.Token);
            }
            finally
            {
                cts.Cancel();
                await thinkingTask;
                Console.WriteLine();
            }

            if (_game.Board.SequenceEqual(boardBefore))
            {
                Console.WriteLine("The AI did not make a valid move. Press any key to continue...");
                Console.ReadKey(true);
            }
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private void DrawBoard()
    {
        Console.WriteLine("Tic-Tac-Toe");
        Console.WriteLine(new string('=', 20));
        Console.WriteLine();

        for (var row = 0; row < 3; row++)
        {
            Console.WriteLine($"  {Cell(_game.Board, 0, row)} | {Cell(_game.Board, 1, row)} | {Cell(_game.Board, 2, row)}");
            if (row < 2)
                Console.WriteLine("  ---+---+---");
        }
    }

    private static char Cell(IReadOnlyList<char> board, int col, int row) => board[row * 3 + col];
}
