using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Deferred;

public interface IDeferredSessionResultStore
{
    Task<SessionResult> WaitForResultAsync(string sessionId, CancellationToken ct = default);
    void SetResult(string sessionId, SessionResult result);
    void SetError(string sessionId, Exception ex);
}
