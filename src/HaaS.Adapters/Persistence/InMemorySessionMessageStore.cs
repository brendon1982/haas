using System.Collections.Concurrent;
using HaaS.Domain.Ports;

namespace HaaS.Adapters.Persistence;

public class InMemorySessionMessageStore : IMessageStore
{
    private readonly ConcurrentDictionary<string, List<string>> _store = new();

    public Task<IReadOnlyList<string>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<string>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<string> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }
}
