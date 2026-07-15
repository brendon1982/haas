using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IMessageStore
{
    Task<IReadOnlyList<DomainMessage>> GetMessagesAsync(string sessionId);
    Task AppendMessagesAsync(string sessionId, IEnumerable<DomainMessage> messages);
    Task<int> GetMessageCountAsync(string sessionId);
}
