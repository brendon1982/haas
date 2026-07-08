using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

public class CliSignalPresenter : ISignalPresenter
{
    public async Task PresentAsync(SessionResult result)
    {
        await Console.Out.WriteLineAsync(result.Output);
    }
}
