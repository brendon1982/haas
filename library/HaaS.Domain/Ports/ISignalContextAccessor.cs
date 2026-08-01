using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalContextAccessor
{
    SignalExecutionContext Current { get; }
}
