using System.Collections.Concurrent;

namespace HaaS.Host.Web;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Type, object>> _sessionStates = new();

    public T GetOrCreate<T>(string sessionId) where T : class, new()
    {
        var states = _sessionStates.GetOrAdd(sessionId, _ => new ConcurrentDictionary<Type, object>());
        return (T)states.GetOrAdd(typeof(T), _ => new T());
    }

    public void Remove<T>(string sessionId)
    {
        if (_sessionStates.TryGetValue(sessionId, out var states))
        {
            states.TryRemove(typeof(T), out _);
        }
    }
}

public class ScopedSessionContext
{
    public string? SessionId { get; set; }
}
