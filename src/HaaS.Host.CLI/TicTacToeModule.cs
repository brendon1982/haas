using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Host.CLI;

public class TicTacToeModule : ICliModule
{
    public string Name => "Tic-Tac-Toe";
    public string Description => "Classic 3-in-a-row game against an AI opponent";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";
        var modelId = Environment.GetEnvironmentVariable("HAAS_MODEL") ?? "gemma4";

        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHaas()
                    .WithSqlitePersistence("tictactoe-data", includeConfig: false)
                    .WithInMemoryConfig(config =>
                    {
                        config.UseOllama();
                        config.UseOpenRouter();
                    })
                    .AddQueuedWorkerPool(1, pool =>
                    {
                        pool.AddSignalSource<TicTacToeSignalSource, CliSignalPresenter>(config =>
                        {
                            config.UseProvider(providerName)
                                .UseModel(modelId)
                                .UseSystemPrompt(GetSystemPrompt())
                                .AddTool("get_board")
                                .AddTool("get_valid_moves")
                                .AddTool("place_marker");
                        });
                    });

                services.AddSingleton<TicTacToeGame>();
            })
            .Build();

        var toolProvider = host.Services.GetRequiredService<IToolProvider>();
        toolProvider.Register<TicTacToeGame>("get_board", "Returns the current Tic-Tac-Toe board as a formatted string.", g => (Func<string>)g.FormatBoard);
        toolProvider.Register<TicTacToeGame>("get_valid_moves", "Returns a comma-separated list of available positions (1-9).", g => (Func<string>)g.FormatValidMoves);
        toolProvider.Register<TicTacToeGame>("place_marker", "Places your O marker at the specified position (1-9). Call this ONCE per turn to make your move.", g => (Func<int, string>)g.PlaceMarker);

        await host.RunAsync(ct);

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
