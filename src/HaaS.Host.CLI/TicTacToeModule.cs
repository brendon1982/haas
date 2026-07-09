using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Host.CLI;

public class TicTacToeModule : ICliModule
{
    private readonly IServiceProvider _provider;

    public TicTacToeModule()
    {
        var services = new ServiceCollection();
        services.AddHaas();
        services.AddHaasInMemoryConfig(cli =>
        {
            cli.UseOllama();
            cli.UseOpenRouter();
        });
        services.AddSignalSources();
        _provider = services.BuildServiceProvider();
    }

    public string Name => "Tic-Tac-Toe";
    public string Description => "Classic 3-in-a-row game against an AI opponent";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "openrouter";
        var modelId = "cohere/north-mini-code:free";
        var game = new TicTacToeGame();

        var toolRegistry = _provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_board", () => game.FormatBoard(), "Returns the current Tic-Tac-Toe board as a formatted string.");
        toolRegistry.Register("get_valid_moves", () => game.FormatValidMoves(), "Returns a comma-separated list of available positions (1-9).");
        toolRegistry.Register("place_marker", (int position) =>
        {
            if (game.HasMovedThisTurn)
                return $"You have already placed your marker this turn. You are O and you already played position {position}. Wait for the next turn.";
            if (!game.TryPlace(position))
                return $"Position {position} is not available. Choose from: {game.FormatValidMoves()}.";
            return $"Placed O at position {position}. Your turn is over. Wait for the player to move before your next turn.";
        }, "Places your O marker at the specified position (1-9). Call this ONCE per turn to make your move.");

        var systemPrompt = """
            You are a Tic-Tac-Toe AI playing as 'O'. Your opponent is 'X'.

            Board layout (positions 1-9):
             1 | 2 | 3
            ---+---+---
             4 | 5 | 6
            ---+---+---
             7 | 8 | 9

            Each turn:
            1. Call `get_board` to see the current state.
            2. Call `get_valid_moves` to check available positions.
            3. Choose a strategic move and call `place_marker` with your chosen position.

            Strategy (in order of priority):
            - Win: take a position that gives you three in a row.
            - Block: take a position that stops X from getting three in a row.
            - Fork: create a situation where you have two winning threats.
            - Block fork: take a position that stops X from creating a fork.
            - Center: take position 5.
            - Opposite corner: if X is in a corner, take the opposite corner.
            - Empty corner: take a corner (1, 3, 7, 9).
            - Edge: take an edge (2, 4, 6, 8).

            Explain your reasoning briefly, then call `place_marker`. You MUST call `place_marker` each turn — do not respond with text alone.
            """;

        var signalSourceConfigRepo = _provider.GetRequiredService<ISignalSourceConfigRepository>();
        await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
            SourceType: "cli",
            Provider: providerName,
            ModelId: modelId,
            SystemPrompt: systemPrompt,
            ToolBelt: new ToolBelt(["get_board", "get_valid_moves", "place_marker"]),
            ThinkingLevel: "on"
        ));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            var useCase = _provider.GetRequiredService<RunSessionUseCase>();
            var presenter = new CapturingPresenter();

            while (!cts.Token.IsCancellationRequested)
            {
                Console.Clear();
                DrawBoard(game.Board);

                if (game.TryGetWinner(out var winner))
                {
                    Console.WriteLine(winner == 'X' ? "You win!" : "AI wins!");
                    break;
                }

                if (game.IsDraw())
                {
                    Console.WriteLine("It's a draw!");
                    break;
                }

                Console.WriteLine();
                Console.Write("Your move (1-9, or 0 to quit): ");
                var input = Console.ReadLine();

                if (input == "0" || string.IsNullOrWhiteSpace(input))
                    break;

                if (!int.TryParse(input, out var pos) || pos < 1 || pos > 9 || game.Board[pos - 1] != ' ')
                {
                    Console.WriteLine("Invalid move. Press any key to try again...");
                    Console.ReadKey(true);
                    continue;
                }

                game.PlacePlayer(pos);

                if (game.TryGetWinner(out _) || game.IsDraw())
                    continue;

                Console.Clear();
                DrawBoard(game.Board);
                Console.WriteLine();
                Console.Write("AI is thinking");
                _ = Console.Out.FlushAsync();

                game.ResetTurn();
                var boardBefore = game.Board.ToArray();
                var signal = new Signal(
                    $"The player (X) just moved at position {pos}. It's your turn (O). Make your move.",
                    "cli",
                    presenter.LastSessionId);

                try
                {
                    await useCase.ExecuteAsync(signal, presenter);

                    if (!string.IsNullOrWhiteSpace(presenter.LastOutput))
                    {
                        Console.WriteLine();
                        Console.WriteLine(presenter.LastOutput);
                    }

                    if (game.Board.SequenceEqual(boardBefore))
                    {
                        Console.WriteLine();
                        Console.WriteLine("The AI did not make a valid move. Continuing...");
                        _ = Console.ReadKey(true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine($"AI error: {ex.Message}");
                    Console.WriteLine("Press any key to continue...");
                    _ = Console.ReadKey(true);
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        Console.WriteLine();
        Console.WriteLine("Game over. Press any key to return to menu...");
        Console.ReadKey(true);
    }

    private static void DrawBoard(IReadOnlyList<char> board)
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

    private static char Cell(IReadOnlyList<char> board, int col, int row) => board[row * 3 + col];

    private sealed class TicTacToeGame
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

    private sealed class CapturingPresenter : ISignalPresenter
    {
        public string? LastSessionId { get; private set; }
        public string? LastOutput { get; private set; }

        public Task PresentAsync(SessionResult result)
        {
            LastSessionId = result.SessionId;
            LastOutput = result.Output;
            return Task.CompletedTask;
        }
    }
}
