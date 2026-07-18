using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Host.CLI.TicTacToe;

public class TicTacToeModule : ICliModule
{
    public string Name => "Tic-Tac-Toe";
    public string Description => "Classic 3-in-a-row game against an AI opponent";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "openrouter";
        var modelId = Environment.GetEnvironmentVariable("HAAS_MODEL") ?? "cohere/north-mini-code:free";

        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHaas()
                    .WithSpectreConsole()
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
                services.AddSingleton<TicTacToeToolHandlers>();
            })
            .Build();

        RegisterTools(host.Services.GetRequiredService<IToolProvider>());

        await host.RunAsync(ct);

        Console.WriteLine();
        Console.WriteLine("Game over. Press any key to return to menu...");
        Console.ReadKey(true);
    }

    private void RegisterTools(IToolProvider toolProvider)
    {
        toolProvider.Register<TicTacToeToolHandlers>("get_board", 
            "Returns the current Tic-Tac-Toe board as a formatted text grid.", 
            h => h.GetBoard);

        toolProvider.Register<TicTacToeToolHandlers>("get_valid_moves", 
            "Returns a list of available positions (1-9) where you can place your marker.", 
            h => h.GetValidMoves);

        toolProvider.Register<TicTacToeToolHandlers>("place_marker", 
            "Places your 'O' marker at the specified position (1-9).", 
            h => h.PlaceMarker);
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
