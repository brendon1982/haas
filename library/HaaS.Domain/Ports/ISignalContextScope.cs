using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalContextScope
{
    IDisposable Push(SignalExecutionContext context);
}
