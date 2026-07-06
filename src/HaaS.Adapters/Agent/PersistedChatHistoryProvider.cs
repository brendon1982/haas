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
        return stored.Select(DeserializeMessage);
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var sessionId = GetSessionId(context.Session);
        if (sessionId is null)
        {
            return;
        }
        var messages = (context.RequestMessages)
            .Concat(context.ResponseMessages ?? [])
            .Select(SerializeMessage);
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
    {
        var dto = JsonSerializer.Deserialize<MessageDto>(data);
        return new ChatMessage(new ChatRole(dto!.Role), dto.Content);
    }

    private static string SerializeMessage(ChatMessage message)
    {
        return JsonSerializer.Serialize(new MessageDto(message.Role.ToString(), message.Text));
    }

    private record MessageDto(string Role, string Content);
}
