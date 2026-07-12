using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalSource
{
    string Type { get; }
    Task ListenAsync(Func<Signal, Task<string>> handler);
    Task ShutdownAsync();
}
