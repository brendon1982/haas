using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IExecutionTarget
{
    Task DeliverAsync(SessionResult result);
}
