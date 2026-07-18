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

    public TicTacToeSignalSource(TicTacToeGame game, CliLayoutManager layoutManager, IHostApplicationLifetime? lifetime = null)
    {
        _game = game;
        _layoutManager = layoutManager;
        _lifetime = lifetime;
    }

    public string Type => "tictactoe";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        await AnsiConsole.Live(_layoutManager.Layout)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (true)
                {
                    UpdateLayout();
                    ctx.Refresh();

                    if (CheckGameOver(ctx))
                    {
                        await Task.Delay(2000); // Give user time to see the result
                        _lifetime?.StopApplication();
                        break;
                    }

                    var position = await GetHumanMoveAsync(ctx);
                    if (position == 0) // Quit
                    {
                        _lifetime?.StopApplication();
                        break;
                    }

                    _game.PlacePlayerMarker(position);

                    if (CheckGameOver(ctx))
                    {
                        UpdateLayout();
                        ctx.Refresh();
                        await Task.Delay(2000);
                        _lifetime?.StopApplication();
                        break;
                    }

                    // Trigger the AI to move by sending a signal through the HaaS engine
                    await TriggerAiMoveAsync(handler, position, ctx);
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
        if (winner != null)
        {
            _layoutManager.AddLog(winner == 'X' ? "[green]You win![/]" : "[red]AI wins![/]");
            return true;
        }

        if (_game.IsDraw())
        {
            _layoutManager.AddLog("[yellow]It's a draw![/]");
            return true;
        }

        return false;
    }

    private async Task<int> GetHumanMoveAsync(LiveDisplayContext ctx)
    {
        var input = string.Empty;
        var validMoves = Enumerable.Range(1, 9)
            .Where(i => _game.IsValidMove(i))
            .ToList();

        while (true)
        {
            var prompt = $"Your move (1-9, 'q' to quit): {input}_";
            _layoutManager.SetInput(prompt);
            ctx.Refresh();

            if (!Console.KeyAvailable)
            {
                await Task.Delay(50);
                continue;
            }

            var key = Console.ReadKey(true);

            // Handle scrolling
            if (key.Key == ConsoleKey.PageUp)
            {
                _layoutManager.Scroll(5);
                ctx.Refresh();
                continue;
            }
            if (key.Key == ConsoleKey.PageDown)
            {
                _layoutManager.Scroll(-5);
                ctx.Refresh();
                continue;
            }
            if (key.Key == ConsoleKey.UpArrow && string.IsNullOrEmpty(input))
            {
                _layoutManager.Scroll(1);
                ctx.Refresh();
                continue;
            }
            if (key.Key == ConsoleKey.DownArrow && string.IsNullOrEmpty(input))
            {
                _layoutManager.Scroll(-1);
                ctx.Refresh();
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (int.TryParse(input, out var pos) && validMoves.Contains(pos))
                {
                    _layoutManager.SetInput(string.Empty);
                    ctx.Refresh();
                    return pos;
                }
                if (input.Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    _layoutManager.SetInput(string.Empty);
                    ctx.Refresh();
                    return 0;
                }
                
                input = string.Empty; // Invalid, reset
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input = input[..^1];
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input += key.KeyChar;
            }
        }
    }

    private async Task TriggerAiMoveAsync(Func<IncomingSignal, Task<ISignalHandle>> handler, int lastPlayerMove, LiveDisplayContext ctx)
    {
        _game.ResetTurn();

        var signal = new IncomingSignal(
            $"The player (X) just moved at position {lastPlayerMove}. It's your turn (O). Make your move.");

        var boardBefore = _game.Board.ToArray();

        _layoutManager.SetBusy(true);
        UpdateLayout();
        ctx.Refresh();

        // We use the already active Live display, so we don't need RunLiveAsync here
        // as it would start a nested Live display which is not supported.
        var handle = await handler(signal);
        await handle.WaitForResultAsync();

        _layoutManager.SetBusy(false);
        UpdateLayout();
        ctx.Refresh();

        if (_game.Board.SequenceEqual(boardBefore))
        {
            _layoutManager.AddLog("[yellow]The AI did not make a valid move.[/]");
        }
    }
}
