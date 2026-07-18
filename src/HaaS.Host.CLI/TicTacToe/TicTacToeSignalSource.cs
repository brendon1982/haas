using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Terminal.Gui;
using Terminal.Gui.Views;

namespace HaaS.Host.CLI.TicTacToe;

public class TicTacToeSignalSource : ISignalSource
{
    private readonly TicTacToeGame _game;
    private readonly GuiLayoutManager _layoutManager;
    private readonly GuiSignalPresenter _presenter;
    private TaskCompletionSource? _tcs;

    public TicTacToeSignalSource(TicTacToeGame game, GuiLayoutManager layoutManager, GuiSignalPresenter presenter)
    {
        _game = game;
        _layoutManager = layoutManager;
        _presenter = presenter;
    }

    public string Type => "tictactoe";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        _tcs = new TaskCompletionSource();
        var view = new TicTacToeView(_presenter, _layoutManager);
        
        view.OnMoveSelected += async pos =>
        {
            if (!_game.IsValidMove(pos)) return;

            _game.PlacePlayerMarker(pos);
            view.UpdateBoard(_game.Board);

            if (CheckGameOver(view))
            {
                _tcs.TrySetResult();
                return;
            }

            view.UpdateStatus("AI is thinking...");
            await TriggerAiMoveAsync(handler, pos, view);
            
            if (CheckGameOver(view))
            {
                _tcs.TrySetResult();
                return;
            }
            
            view.UpdateStatus("Your turn (Select 1-9)");
        };

        view.UpdateBoard(_game.Board);
        _layoutManager.SetMainContent(view);

        await _tcs.Task;
        
        await Task.Delay(2000);
        _layoutManager.SetMainContent(new Label() { Text = "Game Over. Returning to menu..." });
    }

    private bool CheckGameOver(TicTacToeView view)
    {
        var winner = _game.GetWinner();
        if (winner != null)
        {
            view.UpdateStatus(winner == 'X' ? "You win!" : "AI wins!");
            view.UpdateBoard(_game.Board);
            return true;
        }

        if (_game.IsDraw())
        {
            view.UpdateStatus("It's a draw!");
            view.UpdateBoard(_game.Board);
            return true;
        }

        return false;
    }

    private async Task TriggerAiMoveAsync(Func<IncomingSignal, Task<ISignalHandle>> handler, int lastPlayerMove, TicTacToeView view)
    {
        _game.ResetTurn();
        var signal = new IncomingSignal($"The player (X) just moved at position {lastPlayerMove}. It's your turn (O). Make your move.");
        var handle = await handler(signal);
        await handle.WaitForResultAsync();
        view.UpdateBoard(_game.Board);
    }

    public Task ShutdownAsync()
    {
        _tcs?.TrySetResult();
        return Task.CompletedTask;
    }
}
