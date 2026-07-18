using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace HaaS.Host.CLI.Infrastructure;

public sealed class GuiLayoutManager : IDisposable
{
    private readonly IApplication _app;
    private readonly Window _topWindow;
    private readonly Window _mainWindow;
    private readonly Window _logWindow;
    private readonly TextView _logText;
    private View? _currentMainContent;

    public IApplication App => _app;

    public GuiLayoutManager()
    {
        _app = Terminal.Gui.App.Application.Create();
        _app.Init();
        _topWindow = new Window();
        
        _mainWindow = new Window()
        {
            Title = "Content",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(70)
        };

        _logWindow = new Window()
        {
            Title = "Logs",
            X = 0,
            Y = Pos.Bottom(_mainWindow),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _logText = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true
        };

        _logWindow.Add(_logText);
        _topWindow.Add(_mainWindow, _logWindow);
    }

    public void SetMainContent(View view)
    {
        _app.Invoke(() =>
        {
            if (_currentMainContent != null)
            {
                _mainWindow.Remove(_currentMainContent);
            }
            _currentMainContent = view;
            _mainWindow.Add(_currentMainContent);
        });
    }

    public void AddLog(string log)
    {
        _app.Invoke(() =>
        {
            _logText.Text += log + "\n";
            // Scrolling to bottom in V2 might be different, skipping CursorPosition for now to build
        });
    }

    public void Run()
    {
        _app.Run(_topWindow);
    }

    public void Stop()
    {
        _app.RequestStop();
        _app.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }
}
