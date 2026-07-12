using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using Signal = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Host.CLI;

public class ChatSignalSource : ISignalSource
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly IDeferredSessionResultStore _resultStore;

    public ChatSignalSource(IDeferredSessionResultStore resultStore)
        : this(Console.In, Console.Out, resultStore)
    {
    }

    public ChatSignalSource(TextReader input, TextWriter output, IDeferredSessionResultStore resultStore)
    {
        _input = input;
        _output = output;
        _resultStore = resultStore;
    }

    public string Type => "chat";

    public async Task ListenAsync(Func<Signal, Task<string>> handler)
    {
        while (true)
        {
            var line = await _input.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(line))
                break;

            var sessionId = await handler(new Signal(line.Trim(), "chat"));

            // Wait for the worker to finish and present the result
            await _resultStore.WaitForResultAsync(sessionId);

            await _output.WriteAsync("> ");
            await _output.FlushAsync();
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
