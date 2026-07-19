using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISessionRepository
{
    Task SaveAsync(SessionRecord record);
    Task<SessionRecord?> LoadAsync(string sessionId);
}
