namespace HaaS.Host.CLI.Infrastructure;

using Spectre.Console;
using Spectre.Console.Rendering;
using System.Linq;

public sealed class CliLayoutManager
{
    private readonly CliLogSink _logSink;
    private IEnumerable<string> _history = Enumerable.Empty<string>();
    private IRenderable? _mainContent;
    private string _input = string.Empty;
    private bool _isBusy;

    public Layout Layout { get; }

    public CliLayoutManager(CliLogSink logSink)
    {
        _logSink = logSink;
        Layout = new Layout("Root")
            .SplitRows(
                new Layout("Main"),
                new Layout("Logs").Size(10),
                new Layout("Input").Size(3)
            );
    }

    public void SetMainContent(IEnumerable<string> history)
    {
        _history = history;
        _mainContent = null;
        UpdateLayout();
    }

    public void SetMainContent(IRenderable content)
    {
        _mainContent = content;
        _history = Enumerable.Empty<string>();
        UpdateLayout();
    }

    public void SetInput(string input)
    {
        _input = input;
        UpdateLayout();
    }

    public void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UpdateLayout();
    }

    public async Task RunLiveAsync(Func<Task> action)
    {
        _isBusy = true;
        try
        {
            await AnsiConsole.Live(Layout)
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    UpdateLayout();
                    ctx.Refresh();

                    var task = action();

                    while (!task.IsCompleted)
                    {
                        UpdateLayout();
                        ctx.Refresh();
                        await Task.Delay(100);
                    }

                    await task;

                    UpdateLayout();
                    ctx.Refresh();
                });
        }
        finally
        {
            _isBusy = false;
            UpdateLayout();
        }
    }

    private void UpdateLayout()
    {
        var header = _isBusy ? "[blue]Content (AI is thinking...)[/]" : "[blue]Content[/]";
        
        IRenderable content;
        if (_mainContent != null)
        {
            content = _mainContent;
        }
        else
        {
            // Main content (Chat history) - simple "scrolling" by taking last N entries
            var mainHeight = Math.Max(5, AnsiConsole.Console.Profile.Height - 13 - 2);
            var chatRows = _history.TakeLast(mainHeight).Select(h => new Markup(h));
            content = new Rows(chatRows);
        }

        Layout["Main"].Update(
            new Panel(content)
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header(header)
        );

        // Logs - show last 8 entries to fit in Size(10)
        var logRows = _logSink.GetLogs()
            .TakeLast(8)
            .Select(l => new Markup(l));

        Layout["Logs"].Update(
            new Panel(new Rows(logRows))
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header("[yellow]Logs[/]")
        );

        // Input box
        Layout["Input"].Update(
            new Panel(new Markup(_input))
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header("[green]Input[/]")
        );
    }
}
