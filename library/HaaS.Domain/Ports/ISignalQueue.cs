using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalQueue
{
    Task EnqueueAsync(Signal signal, Identity identity);
    Task<QueuedSignal?> DequeueAsync();
    Task AckAsync(string id);
    Task NackAsync(string id);
}
