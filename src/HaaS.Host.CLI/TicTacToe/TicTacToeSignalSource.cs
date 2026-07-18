using System.Linq;
using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace HaaS.Host.CLI.TicTacToe;

/// <summary>
/// A CLI-based SignalSource for Tic-Tac-Toe.
/// This class acts as the "adapter" between the human player (via CLI) and the HaaS engine.
/// It collects human moves, sends signals to trigger the AI, and manages the CLI UI.
/// </summary>
public class TicTacToeSignalSource : ISignalSource
{
    private readonly TicTacToeGame _game;
    private readonly CliLayoutManager _layoutManager;
    private readonly IHostApplicationLifetime? _lifetime;

    public TicTacToeSignalSource(
        TicTacToeGame game, 
        CliLayoutManager layoutManager, 
        IHostApplicationLifetime? lifetime = null)
    {
        _game = game;
        _layoutManager = layoutManager;
        _lifetime = lifetime;
    }

    public string Type => "tictactoe";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        AnsiConsole.Clear();
        await AnsiConsole.Live(_layoutManager.Layout)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                Action refresh = () => ctx.Refresh();
                _layoutManager.OnLayoutUpdated += refresh;
                try
                {
                    while (true)
                    {
                        UpdateLayout();
                        if (CheckGameOver(ctx)) break;

                        var input = await _layoutManager.ReadInputAsync("Your move (1-9, 'q' to quit): ");
                        if (input.Equals("q", StringComparison.OrdinalIgnoreCase) || input == "0")
                        {
                            _lifetime?.StopApplication();
                            break;
                        }

                        if (!int.TryParse(input, out var position) || !_game.IsValidMove(position))
                        {
                            _layoutManager.AddLog("[red]Invalid move. Try again.[/]");
                            continue;
                        }

                        _game.PlacePlayerMarker(position);
                        UpdateLayout();

                        if (CheckGameOver(ctx)) break;

                        // Trigger the AI to move by sending a signal through the HaaS engine
                        await TriggerAiMoveAsync(handler, position, ctx);
                    }
                }
                finally
                {
                    _layoutManager.OnLayoutUpdated -= refresh;
                }
            });
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private void UpdateLayout()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .ShowRowSeparators()
            .HideHeaders();

        table.AddColumn("C1");
        table.AddColumn("C2");
        table.AddColumn("C3");

        var b = _game.Board;
        for (var row = 0; row < 3; row++)
        {
            table.AddRow(
                RenderCell(b[row * 3]),
                RenderCell(b[row * 3 + 1]),
                RenderCell(b[row * 3 + 2])
            );
        }

        _layoutManager.SetMainContent(
            new Panel(Align.Center(table, VerticalAlignment.Middle))
                .Header("Tic-Tac-Toe")
                .Expand()
        );
    }

    private string RenderCell(char c) => c switch
    {
        'X' => "[green]X[/]",
        'O' => "[red]O[/]",
        _ => $"[grey]{c}[/]"
    };

    private bool CheckGameOver(LiveDisplayContext ctx)
    {
        var winner = _game.GetWinner();
        bool isOver = false;

        if (winner != null)
        {
            _layoutManager.AddLog(winner == 'X' ? "[green]You win![/]" : "[red]AI wins![/]");
            isOver = true;
        }
        else if (_game.IsDraw())
        {
            _layoutManager.AddLog("[yellow]It's a draw![/]");
            isOver = true;
        }

        if (isOver)
        {
            UpdateLayout();
            // We don't stop application here immediately, we let the loop handle it
            // but we might want to wait a bit so the user can see it.
            // Actually, the loop will break and finish the StartAsync.
            _lifetime?.StopApplication();
        }

        return isOver;
    }

    private async Task TriggerAiMoveAsync(Func<IncomingSignal, Task<ISignalHandle>> handler, int lastPlayerMove, LiveDisplayContext ctx)
    {
        _game.ResetTurn();

        var signal = new IncomingSignal(
            $"The player (X) just moved at position {lastPlayerMove}. It's your turn (O). Make your move.");

        var boardBefore = _game.Board.ToArray();

        _layoutManager.SetBusy(true);
        UpdateLayout();

        // We use the already active Live display, so we don't need RunLiveAsync here
        // as it would start a nested Live display which is not supported.
        try
        {
            var handle = await handler(signal);
            await handle.WaitForResultAsync();
        }
        catch (Exception)
        {
            // Error already presented by framework, catch to keep loop alive
        }
        finally
        {
            _layoutManager.SetBusy(false);
            UpdateLayout();
        }

        if (_game.Board.SequenceEqual(boardBefore))
        {
            _layoutManager.AddLog("[yellow]The AI did not make a valid move.[/]");
        }
    }
}
