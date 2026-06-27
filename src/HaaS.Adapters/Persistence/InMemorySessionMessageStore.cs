using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Persistence;

public class InMemorySessionMessageStore : ISessionMessageStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _store = new();

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<ChatMessage> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }
}
