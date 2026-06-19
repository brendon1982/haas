using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Signal;

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

    public async Task ListenAsync(Func<Signal, Task> handler)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            string? line;
            while ((line = await _input.ReadLineAsync(token)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    break;

                await handler(new Signal(line.Trim(), "cli"));

                if (token.IsCancellationRequested)
                    break;

                await _output.WriteAsync("> ");
                await _output.FlushAsync();
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
