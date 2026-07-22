using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.Web.Infrastructure;

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
