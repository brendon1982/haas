using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using Signal = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Host.CLI;

public class ChatSignalSource : ISignalSource
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    public ChatSignalSource()
        : this(Console.In, Console.Out)
    {
    }

    public ChatSignalSource(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    public string Type => "chat";

    public async Task ListenAsync(Func<Signal, Task<ISignalHandle>> handler)
    {
        while (true)
        {
            var line = await _input.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(line))
                break;

            var handle = await handler(new Signal(line.Trim(), "chat"));

            // Wait for the worker to finish and present the result
            await handle.WaitForResultAsync();

            await _output.WriteAsync("> ");
            await _output.FlushAsync();
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
