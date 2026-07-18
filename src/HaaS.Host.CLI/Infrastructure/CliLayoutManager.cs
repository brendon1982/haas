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
    private int _scrollOffset;

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

    public void SetHistory(IEnumerable<string> history)
    {
        _history = history;
        _scrollOffset = 0; // Follow the tail on new content
        UpdateLayout();
    }

    public void SetMainContent(IRenderable? content)
    {
        _mainContent = content;
        UpdateLayout();
    }

    public void Scroll(int delta)
    {
        _scrollOffset = Math.Max(0, _scrollOffset + delta);
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

    public void AddLog(string log)
    {
        _logSink.AddLog(log);
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
        
        IRenderable mainArea;
        
        // Prepare History renderable
        var historyList = _history.ToList();
        var mainHeight = Math.Max(5, AnsiConsole.Console.Profile.Height - 13 - 2);
        
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, historyList.Count - 1));
        var chatRows = historyList
            .SkipLast(_scrollOffset)
            .TakeLast(mainHeight)
            .Select(h => new Markup(h));
        
        var historyContent = new Rows(chatRows);
        var historyHeader = "[blue]History[/]";
        if (_scrollOffset > 0)
        {
            historyHeader += $" [yellow](Scrolled up: {_scrollOffset})[/]";
        }

        if (_mainContent != null && historyList.Any())
        {
            // Split Main area into two panels
            mainArea = new Columns(
                new Panel(_mainContent)
                    .Expand()
                    .Border(BoxBorder.Rounded)
                    .Header(header),
                new Panel(historyContent)
                    .Expand()
                    .Border(BoxBorder.Rounded)
                    .Header(historyHeader)
            );
        }
        else if (_mainContent != null)
        {
            mainArea = new Panel(_mainContent)
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header(header);
        }
        else
        {
            mainArea = new Panel(historyContent)
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header(historyHeader);
        }

        Layout["Main"].Update(mainArea);

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
