using System.Linq;
using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
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

    public TicTacToeSignalSource(TicTacToeGame game, CliLayoutManager layoutManager)
    {
        _game = game;
        _layoutManager = layoutManager;
    }

    public string Type => "tictactoe";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        while (true)
        {
            UpdateLayout();

            if (CheckGameOver())
                break;

            var position = GetHumanMove();
            if (position == 0) // Quit
                break;

            _game.PlacePlayerMarker(position);

            if (CheckGameOver())
            {
                UpdateLayout();
                continue;
            }

            // Trigger the AI to move by sending a signal through the HaaS engine
            await TriggerAiMoveAsync(handler, position);
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private void UpdateLayout()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
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

    private bool CheckGameOver()
    {
        var winner = _game.GetWinner();
        if (winner != null)
        {
            AnsiConsole.MarkupLine(winner == 'X' ? "[green]You win![/]" : "[red]AI wins![/]");
            return true;
        }

        if (_game.IsDraw())
        {
            AnsiConsole.MarkupLine("[yellow]It's a draw![/]");
            return true;
        }

        return false;
    }

    private int GetHumanMove()
    {
        var validMoves = Enumerable.Range(1, 9)
            .Where(i => _game.IsValidMove(i))
            .Select(i => i.ToString())
            .ToList();
        
        validMoves.Add("Quit");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Your move:")
                .AddChoices(validMoves));

        if (choice == "Quit")
            return 0;

        return int.Parse(choice);
    }

    private async Task TriggerAiMoveAsync(Func<IncomingSignal, Task<ISignalHandle>> handler, int lastPlayerMove)
    {
        _game.ResetTurn();

        var signal = new IncomingSignal(
            $"The player (X) just moved at position {lastPlayerMove}. It's your turn (O). Make your move.");

        var boardBefore = _game.Board.ToArray();

        await _layoutManager.RunLiveAsync(async () =>
        {
            // We use RunLiveAsync to keep the log pane updating while AI is thinking
            var handle = await handler(signal);
            await handle.WaitForResultAsync();
        });

        if (_game.Board.SequenceEqual(boardBefore))
        {
            AnsiConsole.MarkupLine("[yellow]The AI did not make a valid move. Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }
}
