using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalPresenter
{
    Task PresentAsync(SessionResult result);
    Task PresentErrorAsync(string? sessionId, Exception exception);
}
