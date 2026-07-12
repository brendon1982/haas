using System.Collections.Concurrent;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Deferred;

public class DeferredSessionResultStore : IDeferredSessionResultStore
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<SessionResult>> _results = new();

    public Task<SessionResult> WaitForResultAsync(string sessionId, CancellationToken ct = default)
    {
        var tcs = _results.GetOrAdd(sessionId, _ => new TaskCompletionSource<SessionResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        
        if (ct != default)
        {
            ct.Register(() => tcs.TrySetCanceled());
        }
        
        return tcs.Task;
    }

    public void SetResult(string sessionId, SessionResult result)
    {
        if (_results.TryRemove(sessionId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }

    public void SetError(string sessionId, Exception ex)
    {
        if (_results.TryRemove(sessionId, out var tcs))
        {
            tcs.TrySetException(ex);
        }
    }
}
