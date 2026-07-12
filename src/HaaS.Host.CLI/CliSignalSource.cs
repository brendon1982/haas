using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Host.CLI;

public class CliSignalSource : ISignalSource
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly IDeferredSessionResultStore _resultStore;
    private CancellationTokenSource? _cts;

    public CliSignalSource(IDeferredSessionResultStore resultStore)
        : this(Console.In, Console.Out, resultStore)
    {
    }

    public CliSignalSource(TextReader input, TextWriter output, IDeferredSessionResultStore resultStore)
    {
        _input = input;
        _output = output;
        _resultStore = resultStore;
    }

    public string Type => "cli";

    public async Task ListenAsync(Func<SignalValue, Task<string>> handler)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            while (await _input.ReadLineAsync(token) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    break;

                var sessionId = await handler(new SignalValue(line.Trim(), "cli"));

                // Wait for the worker to finish and present the result
                await _resultStore.WaitForResultAsync(sessionId, token);

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
