using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SignalValue = HaaS.Domain.ValueObjects.Signal;
using SessionResultValue = HaaS.Domain.ValueObjects.SessionResult;

namespace HaaS.Adapters.Agent;

public class MicrosoftAgentFrameworkStrategy : IAgentStrategy
{
    private readonly IChatClientFactory _chatClientFactory;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageStore _messageStore;
    private readonly IToolRegistry _toolRegistry;

    public MicrosoftAgentFrameworkStrategy(
        IChatClientFactory chatClientFactory,
        ISessionRepository sessionRepository,
        IMessageStore messageStore,
        IToolRegistry toolRegistry)
    {
        _chatClientFactory = chatClientFactory;
        _sessionRepository = sessionRepository;
        _messageStore = messageStore;
        _toolRegistry = toolRegistry;
    }

    public async Task<SessionResultValue> ExecuteAsync(SignalValue signal, string sessionId)
    {
        var record = await _sessionRepository.LoadAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var config = record.ToConfig();

        if (!string.IsNullOrEmpty(config.SystemPrompt))
        {
            var count = await _messageStore.GetMessageCountAsync(sessionId);
            if (count == 0)
            {
                await _messageStore.AppendMessagesAsync(
                    sessionId,
                    [JsonSerializer.Serialize(new ChatMessage(new ChatRole("system"), config.SystemPrompt))]);
            }
        }

        if (!_chatClientFactory.CanCreate(config.Provider))
        {
            throw new InvalidOperationException(
                $"No chat client available for provider '{config.Provider}'.");
        }

        var chatClient = await _chatClientFactory.CreateAsync(config.Provider, config.ModelId);

        var chatOptions = new ChatOptions();
        chatOptions.AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = true };
        if (config.ToolBelt.Tools.Count > 0)
        {
            chatOptions.Tools = _toolRegistry.GetTools(config.ToolBelt.Tools).ToList();
        }

        if (config.ReplyTool is not null)
        {
            chatOptions.ToolMode = ChatToolMode.RequireSpecific(config.ReplyTool);
        }
        else if (chatOptions.Tools?.Count > 0)
        {
            chatOptions.ToolMode = ChatToolMode.RequireAny;
        }

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "HaaSAgent",
                ChatOptions = chatOptions,
                ChatHistoryProvider = new PersistedChatHistoryProvider(_messageStore),
            });

        var session = await agent.CreateSessionAsync();
        session.StateBag.SetValue(PersistedChatHistoryProvider.SessionIdKey, sessionId);

        ChatMessage[] messages = [new(ChatRole.User, signal.Payload)];

        var response = await agent.RunAsync(messages, session);

        return new SessionResultValue(
            Output: response.Text,
            SessionId: sessionId);
    }
}
