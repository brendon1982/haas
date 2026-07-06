using System.Text.Json;
using HaaS.Domain.Ports;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public class PersistedChatHistoryProvider : ChatHistoryProvider
{
    public const string SessionIdKey = "haas_session_id";
    private readonly IMessageStore _messageStore;

    public PersistedChatHistoryProvider(IMessageStore messageStore)
    {
        _messageStore = messageStore;
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        var sessionId = GetSessionId(context.Session);
        if (sessionId is null)
        {
            return [];
        }

        var stored = await _messageStore.GetMessagesAsync(sessionId);
        return stored.Select(DeserializeMessage).OrderBy(o => o.CreatedAt);
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var sessionId = GetSessionId(context.Session);
        if (sessionId is null)
        {
            return;
        }
        var timestamp = DateTimeOffset.UtcNow;
        var messages = (context.RequestMessages)
            .Concat(context.ResponseMessages ?? [])
            .Select(m =>
            {
                m.CreatedAt ??= timestamp;
                return SerializeMessage(m);
            });
        await _messageStore.AppendMessagesAsync(sessionId, messages);
    }

    private static string? GetSessionId(AgentSession? session)
    {
        if (session?.StateBag?.TryGetValue<string>(SessionIdKey, out var id) == true)
        {
            return id;
        }

        return null;
    }

    private static ChatMessage DeserializeMessage(string data)
        => JsonSerializer.Deserialize<ChatMessage>(data)!;

    private static string SerializeMessage(ChatMessage message)
        => JsonSerializer.Serialize(message);
}
