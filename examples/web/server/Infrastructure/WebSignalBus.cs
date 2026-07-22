using System.Threading.Channels;
using System.Collections.Concurrent;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.Web.Infrastructure;

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
