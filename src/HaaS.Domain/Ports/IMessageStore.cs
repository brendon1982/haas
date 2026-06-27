using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IMessageStore
{
    Task<IReadOnlyList<ChatMessageData>> GetMessagesAsync(string sessionId);
    Task AppendMessagesAsync(string sessionId, IEnumerable<ChatMessageData> messages);
}
