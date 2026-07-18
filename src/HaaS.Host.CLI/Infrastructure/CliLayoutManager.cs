using Spectre.Console;
using Spectre.Console.Rendering;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace HaaS.Host.CLI.Infrastructure;

public sealed class CliLayoutManager
{
    private readonly CliLogSink _logSink;
    private readonly List<string> _history = new();
    private readonly List<IRenderable> _cachedLines = new();
    private int _lastProcessedHistoryCount = 0;
    private int _lastWidth = 0;

    private IRenderable? _mainContent;
    private string _input = string.Empty;
    private bool _isBusy;
    private int _scrollOffset;

    public event Action? OnLayoutUpdated;

    public CliLogSink LogSink => _logSink;
    public Layout Layout { get; }

    public CliLayoutManager(CliLogSink logSink)
    {
        _logSink = logSink;
        _logSink.OnLogAdded += UpdateLayout;
        Layout = new Layout("Root")
            .SplitRows(
                new Layout("Main"),
                new Layout("Logs").Size(10),
                new Layout("Input").Size(3)
            );
    }

    public void SetHistory(IEnumerable<string> history)
    {
        var newHistory = history.ToList();
        _history.Clear();
        _history.AddRange(newHistory);
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
                    // Refresh initially
                    UpdateLayout();
                    ctx.Refresh();

                    // Wire up layout updates to trigger refresh during the live display
                    Action refresh = () => ctx.Refresh();
                    OnLayoutUpdated += refresh;

                    try
                    {
                        await action();
                    }
                    finally
                    {
                        OnLayoutUpdated -= refresh;
                    }

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
        OnLayoutUpdated?.Invoke();
        var headerText = _isBusy ? "Content (AI is thinking...)" : "Content";
        var header = $"[blue]{headerText}[/]";
        
        IRenderable mainArea;
        
        // Prepare History renderable
        var consoleHeight = AnsiConsole.Console.Profile.Height;
        var consoleWidth = AnsiConsole.Console.Profile.Width;
        var mainHeight = Math.Max(5, consoleHeight - 13 - 2);
        var mainWidth = Math.Max(10, consoleWidth - 4);
        if (_mainContent != null && _history.Any()) mainWidth /= 2;

        // Invalidate cache if width changed
        if (mainWidth != _lastWidth)
        {
            _cachedLines.Clear();
            _lastProcessedHistoryCount = 0;
            _lastWidth = mainWidth;
        }

        // Incrementally process new history items
        if (_history.Count > _lastProcessedHistoryCount)
        {
            for (int i = _lastProcessedHistoryCount; i < _history.Count; i++)
            {
                _cachedLines.AddRange(SplitIntoLines(_history[i], mainWidth));
            }
            _lastProcessedHistoryCount = _history.Count;
        }
        else if (_history.Count < _lastProcessedHistoryCount)
        {
            // History was cleared or reduced
            _cachedLines.Clear();
            foreach (var msg in _history)
            {
                _cachedLines.AddRange(SplitIntoLines(msg, mainWidth));
            }
            _lastProcessedHistoryCount = _history.Count;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _cachedLines.Count - 1));

        var visibleLines = _cachedLines
            .SkipLast(_scrollOffset)
            .TakeLast(mainHeight);
        
        var historyContent = new Rows(visibleLines);
        var historyHeaderText = _isBusy ? "History (AI is thinking...)" : "History";
        var historyHeader = $"[blue]{historyHeaderText}[/]";
        if (_scrollOffset > 0)
        {
            historyHeader += $" [yellow](Scrolled up: {_scrollOffset} lines)[/]";
        }

        if (_mainContent != null && _history.Any())
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

    private IEnumerable<IRenderable> SplitIntoLines(string markup, int width)
    {
        var lines = markup.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            // Simplistic wrapping that preserves prefix tags
            // Our messages usually look like: [color]User:[/] Message
            var parts = Regex.Split(line, @"(\[.*?\])");
            
            var currentLineMarkup = new StringBuilder();
            int currentLineLength = 0;

            foreach (var part in parts)
            {
                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    currentLineMarkup.Append(part);
                    continue;
                }

                var text = part;
                while (text.Length > 0)
                {
                    int spaceLeft = width - currentLineLength;
                    if (spaceLeft <= 0)
                    {
                        yield return new Markup(currentLineMarkup.ToString());
                        currentLineMarkup.Clear();
                        currentLineLength = 0;
                        spaceLeft = width;
                    }

                    int toTake = Math.Min(text.Length, spaceLeft);
                    currentLineMarkup.Append(text.Substring(0, toTake).Replace("[", "[[").Replace("]", "]]"));
                    text = text.Substring(toTake);
                    currentLineLength += toTake;
                }
            }

            if (currentLineMarkup.Length > 0)
            {
                yield return new Markup(currentLineMarkup.ToString());
            }
        }
    }
}
