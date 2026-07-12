using HaaS.Domain.Ports;
using HaaS.Adapters.Agent;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Host.CLI;

public class TicTacToeModule : ICliModule
{
    private readonly TicTacToeGame _game;

    public TicTacToeModule()
    {
        _game = new TicTacToeGame();
    }

    public string Name => "Tic-Tac-Toe";
    public string Description => "Classic 3-in-a-row game against an AI opponent";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";
        var modelId = Environment.GetEnvironmentVariable("HAAS_MODEL") ?? "gemma4";

        var services = new ServiceCollection();
        services.AddHaas()
            .WithInMemoryConfig(config =>
            {
                config.UseOllama();
                config.UseOpenRouter();
            })
            .WithSqlitePersistence("data")
            .WithWorkerPool(1)
            .AddSignalSource<TicTacToeSignalSource, CliSignalPresenter>(config =>
            {
                config.UseProvider(providerName)
                    .UseModel(modelId)
                    .UseSystemPrompt(GetSystemPrompt())
                    .AddTool("get_board")
                    .AddTool("get_valid_moves")
                    .AddTool("place_marker");
            });

        services.AddSingleton(_game);
        var provider = services.BuildServiceProvider();

        // Start background workers
        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(ct);
        }

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_board", () => _game.FormatBoard(), "Returns the current Tic-Tac-Toe board as a formatted string.");
        toolRegistry.Register("get_valid_moves", () => _game.FormatValidMoves(), "Returns a comma-separated list of available positions (1-9).");
        toolRegistry.Register("place_marker", (int position) =>
        {
            if (_game.HasMovedThisTurn)
                return $"You have already placed your marker this turn. You are O and you already played position {position}. Wait for the next turn.";
            if (!_game.TryPlace(position))
                return $"Position {position} is not available. Choose from: {_game.FormatValidMoves()}.";
            return $"Placed O at position {position}. Your turn is over. Wait for the player to move before your next turn.";
        }, "Places your O marker at the specified position (1-9). Call this ONCE per turn to make your move.");

        var engine = provider.GetRequiredService<IHaasEngine>();

        try
        {
            await engine.RunAsync(ct);
        }
        finally
        {
            foreach (var service in hostedServices)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Game over. Press any key to return to menu...");
        Console.ReadKey(true);
    }

    private string GetSystemPrompt() => """
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
}
