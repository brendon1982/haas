using System.Threading.Channels;
using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.Web;

public class WebSignalBus
{
    private readonly ConcurrentDictionary<string, Channel<IncomingSignal>> _channels = new();

    public async Task PushAsync(string type, IncomingSignal signal)
    {
        var channel = _channels.GetOrAdd(type, _ => Channel.CreateUnbounded<IncomingSignal>());
        await channel.Writer.WriteAsync(signal);
    }

    public IAsyncEnumerable<IncomingSignal> Subscribe(string type)
    {
        var channel = _channels.GetOrAdd(type, _ => Channel.CreateUnbounded<IncomingSignal>());
        return channel.Reader.ReadAllAsync();
    }
}

public class WebSignalSource : ISignalSource
{
    private readonly WebSignalBus _bus;
    public string Type { get; }

    public WebSignalSource(string type, WebSignalBus bus)
    {
        Type = type;
        _bus = bus;
    }

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        await foreach (var signal in _bus.Subscribe(Type))
        {
            try
            {
                await handler(signal);
            }
            catch (Exception)
            {
                // Log error
            }
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}

public class ChatWebSignalSource : WebSignalSource
{
    public ChatWebSignalSource(WebSignalBus bus) : base("chat", bus) { }
}

public class TicTacToeWebSignalSource : WebSignalSource
{
    public TicTacToeWebSignalSource(WebSignalBus bus) : base("tictactoe", bus) { }
}
