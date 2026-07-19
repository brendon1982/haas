using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Persistence;

public class InMemorySessionMessageStore : IMessageStore
{
    private readonly ConcurrentDictionary<string, List<DomainMessage>> _store = new();

    public Task<IReadOnlyList<DomainMessage>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<DomainMessage>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<DomainMessage>>(Array.Empty<DomainMessage>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<DomainMessage> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }

    public Task<int> GetMessageCountAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult(messages.Count);
        }

        return Task.FromResult(0);
    }
}
