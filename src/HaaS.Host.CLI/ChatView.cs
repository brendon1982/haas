using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Input;
using HaaS.Host.CLI.Infrastructure;

namespace HaaS.Host.CLI;

public class ChatView : Window
{
    private readonly TextView _history;
    private readonly TextField _input;
    private readonly Label _prompt;
    private readonly GuiSignalPresenter _presenter;
    private readonly GuiLayoutManager _layoutManager;

    public event Action<string>? OnMessageSent;

    public ChatView(GuiSignalPresenter presenter, GuiLayoutManager layoutManager)
    {
        Title = "AI Chat";
        _presenter = presenter;
        _layoutManager = layoutManager;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        _history = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            ReadOnly = true
        };

        _prompt = new Label()
        {
            Text = "> ",
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = 2,
            Height = 1
        };

        _input = new TextField()
        {
            Text = "",
            X = Pos.Right(_prompt),
            Y = Pos.Top(_prompt),
            Width = Dim.Fill(),
            Height = 1
        };

        // In V2, try using the KeyDown or similar if Accepting is not there
        _input.KeyDown += (sender, args) =>
        {
            if (args == Key.Enter)
            {
                var text = _input.Text.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    OnMessageSent?.Invoke(text);
                    _input.Text = "";
                }
            }
        };

        Add(_input, _history, _prompt);

        _presenter.OnHistoryUpdated(history =>
        {
            _layoutManager.App.Invoke(() =>
            {
                _history.Text = string.Join("\n", history);
            });
        });
    }

    public void FocusInput() => _input.SetFocus();
}
