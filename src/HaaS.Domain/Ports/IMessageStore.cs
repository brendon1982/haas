namespace HaaS.Domain.Ports;

public interface IMessageStore
{
    Task<IReadOnlyList<string>> GetMessagesAsync(string sessionId);
    Task AppendMessagesAsync(string sessionId, IEnumerable<string> messages);
}
