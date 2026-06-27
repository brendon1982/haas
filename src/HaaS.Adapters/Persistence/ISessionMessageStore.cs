using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Persistence;

public interface ISessionMessageStore
{
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string sessionId);
    Task AppendMessagesAsync(string sessionId, IEnumerable<ChatMessage> messages);
}
