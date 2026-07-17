using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
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
        if (sessionId is null) return [];

        var stored = await _messageStore.GetMessagesAsync(sessionId);
        return stored.Select(MapToChatMessage);
    }

    private static ChatMessage MapToChatMessage(DomainMessage message)
    {
        if (!string.IsNullOrEmpty(message.Payload))
        {
            try
            {
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(message.Payload);
                if (chatMessage is not null) return chatMessage;
            }
            catch
            {
                // Fallback to basic reconstruction
            }
        }

        return new ChatMessage(new ChatRole(message.Role), message.Content) 
        { 
            CreatedAt = message.Timestamp 
        };
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var sessionId = GetSessionId(context.Session);
        if (sessionId is null) return;

        var timestamp = DateTimeOffset.UtcNow;
        var messages = context.RequestMessages
            .Concat(context.ResponseMessages ?? [])
            .Select(m => MapToDomainMessage(m, timestamp));
        
        await _messageStore.AppendMessagesAsync(sessionId, messages);
    }

    private static DomainMessage MapToDomainMessage(ChatMessage message, DateTimeOffset defaultTimestamp)
    {
        return new DomainMessage(
            message.Role.Value, 
            message.Text ?? string.Empty, 
            message.CreatedAt ?? defaultTimestamp,
            JsonSerializer.Serialize(message));
    }

    private static string? GetSessionId(AgentSession? session)
    {
        if (session?.StateBag?.TryGetValue<string>(SessionIdKey, out var id) == true)
        {
            return id;
        }

        return null;
    }
}
