using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Spectre.Console;

namespace HaaS.Host.CLI;

public class CliSignalPresenter : ISignalPresenter
{
    private readonly CliLayoutManager _layoutManager;
    private readonly List<string> _history = new();

    public CliSignalPresenter(CliLayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
    }

    public string? LastSessionId { get; private set; }

    public Task PresentProcessingAsync(string sessionId) => Task.CompletedTask;

    public Task PresentAsync(SessionResult result)
    {
        LastSessionId = result.SessionId;
        _history.Add($"[blue]Assistant:[/] {Markup.Escape(result.Output)}");
        UpdateLayout();
        return Task.CompletedTask;
    }

    public Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        LastSessionId = sessionId;
        _history.Add($"[red]Error:[/] {Markup.Escape(exception.Message)}");
        UpdateLayout();
        return Task.CompletedTask;
    }

    public void AddUserMessage(string message)
    {
        _history.Add($"[green]User:[/] {Markup.Escape(message)}");
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        _layoutManager.SetHistory(_history);
    }
}
