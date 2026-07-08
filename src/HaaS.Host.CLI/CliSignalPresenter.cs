using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

public class CliSignalPresenter : ISignalPresenter
{
    public string? LastSessionId { get; private set; }

    public Task PresentAsync(SessionResult result)
    {
        LastSessionId = result.SessionId;
        Console.Out.WriteLine(result.Output);
        return Task.CompletedTask;
    }
}
