using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IDeferredSessionResultStore
{
    Task<SessionResult> WaitForResultAsync(string sessionId, CancellationToken ct = default);
    void SetResult(string sessionId, SessionResult result);
    void SetError(string sessionId, Exception ex);
}
