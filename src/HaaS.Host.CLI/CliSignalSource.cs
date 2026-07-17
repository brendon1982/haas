using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.CLI;

public class CliSignalSource : ISignalSource
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private CancellationTokenSource? _cts;
    public CliSignalSource()
        : this(Console.In, Console.Out)
    {
    }

    public CliSignalSource(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    public string Type => "cli";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            while (await _input.ReadLineAsync(token) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    break;

                var handle = await handler(new IncomingSignal(line.Trim()));

                // Wait for the worker to finish and present the result
                await handle.WaitForResultAsync(token);

                if (token.IsCancellationRequested)
                    break;

                await _output.WriteAsync("> ");
                await _output.FlushAsync(token);
            }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    public Task ShutdownAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}
