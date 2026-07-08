using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OpenAI;

namespace HaaS.Host.CLI;

public class TicTacToeModule : ICliModule
{
    public string Name => "Tic-Tac-Toe";
    public string Description => "Classic 3-in-a-row game against an AI opponent";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var services = new ServiceCollection();
        services.AddHaasCore();
        var provider = services.BuildServiceProvider();

        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";
        var modelId = "gemma4:12b";

        var configRepo = provider.GetRequiredService<IProviderConfigRepository>();
        await configRepo.SaveAsync(new ProviderConfig("ollama", "http://localhost:11434"));
        var openRouterApiKey = Environment.GetEnvironmentVariable("HAAS_OPENROUTER_API_KEY");
        var openRouterEndpoint = Environment.GetEnvironmentVariable("HAAS_OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1";
        await configRepo.SaveAsync(new ProviderConfig("openrouter", openRouterEndpoint, openRouterApiKey));

        var board = new char[9];
        Array.Fill(board, ' ');

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_board", () => FormatBoard(board), "Returns the current Tic-Tac-Toe board as a formatted string.");
        toolRegistry.Register("get_valid_moves", () => FormatValidMoves(board), "Returns a comma-separated list of available positions (1-9).");
        toolRegistry.Register("place_marker", (int position) =>
        {
            if (position < 1 || position > 9 || board[position - 1] != ' ')
                return $"Position {position} is not available. Choose from: {FormatValidMoves(board)}.";
            board[position - 1] = 'O';
            return $"Placed O at position {position}.";
        }, "Places your O marker at the specified position (1-9). Call this to make your move.");

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

        var signalSourceConfigRepo = provider.GetRequiredService<ISignalSourceConfigRepository>();
        await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
            SourceType: "cli",
            Provider: providerName,
            ModelId: modelId,
            SystemPrompt: systemPrompt,
            ToolBelt: new ToolBelt(["get_board", "get_valid_moves", "place_marker"]),
            ThinkingLevel: "off"
        ));

        var clientFactory = provider.GetRequiredService<ChatClientFactory>();
        clientFactory.Register("ollama",
            (providerConfig, mdlId) => new OllamaApiClient(new Uri(providerConfig.Endpoint), mdlId),
            (options, config) =>
            {
                if (config.ThinkingLevel is not null and not "off")
                    options.AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = true };
            });

        clientFactory.Register("openrouter",
            (providerConfig, mdlId) =>
            {
                var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(providerConfig.Endpoint) };
                var credential = new System.ClientModel.ApiKeyCredential(providerConfig.ApiKey!);
                var chatClient = new OpenAI.Chat.ChatClient(mdlId, credential, openAiOptions);
                return chatClient.AsIChatClient();
            });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            var useCase = provider.GetRequiredService<RunSessionUseCase>();
            var presenter = new CapturingPresenter();

            while (!cts.Token.IsCancellationRequested)
            {
                Console.Clear();
                DrawBoard(board);

                if (TryGetWinner(board, out var winner))
                {
                    Console.WriteLine(winner == 'X' ? "You win!" : "AI wins!");
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

                if (TryGetWinner(board, out _) || IsDraw(board))
                    continue;

                Console.WriteLine();
                Console.Write("AI is thinking");
                _ = Console.Out.FlushAsync();

                var boardBefore = board.ToArray();
                var signal = new Signal(
                    $"The player (X) just moved at position {pos}.\n\nCurrent board:\n{FormatBoard(board)}\n\nIt's your turn (O). Make your move.",
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

                    if (board.AsSpan().SequenceEqual(boardBefore))
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

    private static string FormatBoard(char[] board)
    {
        return $"  {Cell(board, 0, 0)} | {Cell(board, 1, 0)} | {Cell(board, 2, 0)}\n" +
               $"  ---+---+---\n" +
               $"  {Cell(board, 0, 1)} | {Cell(board, 1, 1)} | {Cell(board, 2, 1)}\n" +
               $"  ---+---+---\n" +
               $"  {Cell(board, 0, 2)} | {Cell(board, 1, 2)} | {Cell(board, 2, 2)}";
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

    private static string FormatValidMoves(char[] board)
    {
        var positions = new List<int>();
        for (var i = 0; i < board.Length; i++)
            if (board[i] == ' ')
                positions.Add(i + 1);

        return string.Join(", ", positions);
    }

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
