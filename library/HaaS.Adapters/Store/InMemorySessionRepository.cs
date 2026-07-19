using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, SessionRecord> _store = new();

    public Task SaveAsync(SessionRecord record)
    {
        _store[record.SessionId] = record;
        return Task.CompletedTask;
    }

    public Task<SessionRecord?> LoadAsync(string sessionId)
    {
        _store.TryGetValue(sessionId, out var record);
        return Task.FromResult<SessionRecord?>(record);
    }
}
