using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;

namespace HaaS.Host.CLI;

public class GuiSignalPresenter : ISignalPresenter
{
    private readonly GuiLayoutManager _layoutManager;
    private readonly List<string> _history = new();
    private Action<IEnumerable<string>>? _onHistoryUpdated;

    public GuiSignalPresenter(GuiLayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
    }

    public string? LastSessionId { get; private set; }

    public void OnHistoryUpdated(Action<IEnumerable<string>> action)
    {
        _onHistoryUpdated = action;
        _onHistoryUpdated(_history);
    }

    public Task PresentAsync(SessionResult result)
    {
        LastSessionId = result.SessionId;
        _history.Add($"Assistant: {result.Output}");
        _onHistoryUpdated?.Invoke(_history);
        return Task.CompletedTask;
    }

    public void AddUserMessage(string message)
    {
        _history.Add($"User: {message}");
        _onHistoryUpdated?.Invoke(_history);
    }

    public void ClearHistory()
    {
        _history.Clear();
        _onHistoryUpdated?.Invoke(_history);
    }
}
