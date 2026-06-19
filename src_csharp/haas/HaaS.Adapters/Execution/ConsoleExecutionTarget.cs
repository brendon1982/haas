using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Execution;

public class ConsoleExecutionTarget : IExecutionTarget
{
    public async Task DeliverAsync(SessionResult result)
    {
        await Console.Out.WriteLineAsync(string.Empty);
        await Console.Out.WriteLineAsync($"[Session {result.SessionId}]");
        await Console.Out.WriteLineAsync(result.Output);
    }
}
