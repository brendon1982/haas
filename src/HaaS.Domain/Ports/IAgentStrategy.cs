using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IAgentStrategy
{
    Task ExecuteAsync(Signal signal, string sessionId, ISignalPresenter presenter);
}
