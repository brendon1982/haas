using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalQueue
{
    Task EnqueueAsync(SignalEnvelope envelope);
    Task<QueuedSignal?> DequeueAsync();
    Task AckAsync(string id);
    Task NackAsync(string id, string? error = null);
}
