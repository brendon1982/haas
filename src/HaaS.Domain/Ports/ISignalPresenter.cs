using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalPresenter
{
    Task PresentAsync(SessionResult result);
}
