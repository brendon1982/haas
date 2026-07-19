using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IAgentStrategy
{
    Task<SessionResult> ExecuteAsync(Signal signal, string sessionId, ISignalPresenter presenter);
}
