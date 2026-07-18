namespace HaaS.Host.CLI.Infrastructure;

using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class CliLayoutManager
{
    private readonly CliLogSink _logSink;
    private IRenderable? _mainContent;
    private bool _isBusy;

    public Layout Layout { get; }

    public CliLayoutManager(CliLogSink logSink)
    {
        _logSink = logSink;
        Layout = new Layout("Root")
            .SplitRows(
                new Layout("Main"),
                new Layout("Logs").Size(10)
            );
    }

    public void SetMainContent(IRenderable content)
    {
        _mainContent = content;
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
        Layout["Main"].Update(
            new Panel(_mainContent ?? new Text("No content"))
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header(header)
        );

        var logRows = _logSink.GetLogs()
            .Select(l => new Markup(l));

        Layout["Logs"].Update(
            new Panel(new Rows(logRows))
                .Expand()
                .Border(BoxBorder.Rounded)
                .Header("[yellow]Logs[/]")
        );
    }
}
