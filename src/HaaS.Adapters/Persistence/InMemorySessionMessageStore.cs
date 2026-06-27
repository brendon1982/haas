using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Persistence;

public class InMemorySessionMessageStore : IMessageStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessageData>> _store = new();

    public Task<IReadOnlyList<ChatMessageData>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<ChatMessageData>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<ChatMessageData>>(Array.Empty<ChatMessageData>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<ChatMessageData> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }
}
