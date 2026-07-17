using System.Linq;
using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.CLI.TicTacToe;

/// <summary>
/// A CLI-based SignalSource for Tic-Tac-Toe.
/// This class acts as the "adapter" between the human player (via CLI) and the HaaS engine.
/// It collects human moves, sends signals to trigger the AI, and manages the CLI UI.
/// </summary>
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
            RefreshUi();

            if (CheckGameOver())
                break;

            var position = GetHumanMove();
            if (position == 0) // Quit
                break;

            _game.PlacePlayerMarker(position);

            if (CheckGameOver())
                continue;

            // Trigger the AI to move by sending a signal through the HaaS engine
            await TriggerAiMoveAsync(handler, position);
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private void RefreshUi()
    {
        Console.Clear();
        Console.WriteLine("Tic-Tac-Toe");
        Console.WriteLine(new string('=', 20));
        Console.WriteLine();

        var b = _game.Board;
        for (var row = 0; row < 3; row++)
        {
            Console.WriteLine($"  {b[row * 3]} | {b[row * 3 + 1]} | {b[row * 3 + 2]}");
            if (row < 2)
                Console.WriteLine("  ---+---+---");
        }
        Console.WriteLine();
    }

    private bool CheckGameOver()
    {
        var winner = _game.GetWinner();
        if (winner != null)
        {
            Console.WriteLine(winner == 'X' ? "You win!" : "AI wins!");
            return true;
        }

        if (_game.IsDraw())
        {
            Console.WriteLine("It's a draw!");
            return true;
        }

        return false;
    }

    private int GetHumanMove()
    {
        while (true)
        {
            Console.Write("Your move (1-9, or 0 to quit): ");
            var input = Console.ReadLine();

            if (input == "0" || string.IsNullOrWhiteSpace(input))
                return 0;

            if (int.TryParse(input, out var pos) && _game.IsValidMove(pos))
                return pos;

            Console.WriteLine("Invalid move. Try again.");
        }
    }

    private async Task TriggerAiMoveAsync(Func<IncomingSignal, Task<ISignalHandle>> handler, int lastPlayerMove)
    {
        _game.ResetTurn();

        var signal = new IncomingSignal(
            $"The player (X) just moved at position {lastPlayerMove}. It's your turn (O). Make your move.");

        var boardBefore = _game.Board.ToArray();

        Console.Write("AI is thinking");
        using var cts = new CancellationTokenSource();
        var thinkingTask = ShowThinkingProgress(cts.Token);

        try
        {
            // Send the signal to the engine and wait for the result
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

    private async Task ShowThinkingProgress(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Console.Write(".");
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) { }
    }
}
