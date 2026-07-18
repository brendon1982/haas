using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using HaaS.Host.CLI.Infrastructure;

namespace HaaS.Host.CLI.TicTacToe;

public class TicTacToeView : View
{
    private readonly Button[] _buttons = new Button[9];
    private readonly Label _status;
    private readonly TextView _history;
    private readonly GuiLayoutManager _layoutManager;

    public event Action<int>? OnMoveSelected;

    public TicTacToeView(GuiSignalPresenter presenter, GuiLayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var boardFrame = new Window()
        {
            Title = "Board",
            X = 0,
            Y = 0,
            Width = 15,
            Height = 10
        };

        for (int i = 0; i < 9; i++)
        {
            int index = i;
            int row = i / 3;
            int col = i % 3;

            _buttons[i] = new Button()
            {
                Text = (i + 1).ToString(),
                X = col * 4,
                Y = row * 2,
                Width = 3,
                Height = 1
            };

            _buttons[i].Accepted += (s, e) => OnMoveSelected?.Invoke(index + 1);
            boardFrame.Add(_buttons[i]);
        }

        _status = new Label()
        {
            Text = "Your turn (Select 1-9)",
            X = 0,
            Y = Pos.Bottom(boardFrame),
            Width = Dim.Fill(),
            Height = 1
        };

        _history = new TextView()
        {
            X = Pos.Right(boardFrame) + 2,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true
        };

        Add(boardFrame, _status, _history);

        presenter.OnHistoryUpdated(history =>
        {
            _layoutManager.App.Invoke(() =>
            {
                _history.Text = string.Join("\n", history);
            });
        });
    }

    public void UpdateBoard(IReadOnlyList<char> board)
    {
        _layoutManager.App.Invoke(() =>
        {
            for (int i = 0; i < 9; i++)
            {
                var val = board[i].ToString();
                _buttons[i].Text = val == " " ? (i + 1).ToString() : val;
                _buttons[i].Enabled = val == " ";
            }
        });
    }

    public void UpdateStatus(string status) 
    {
        _layoutManager.App.Invoke(() => _status.Text = status);
    }
}
