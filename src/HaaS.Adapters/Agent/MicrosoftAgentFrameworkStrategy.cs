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

        if (!_chatClientFactory.CanCreate(config.Provider))
        {
            throw new InvalidOperationException(
                $"No chat client available for provider '{config.Provider}'.");
        }

        var chatClient = await _chatClientFactory.CreateAsync(config.Provider, config.ModelId);

        var chatOptions = new ChatOptions();
        if (config.ToolBelt.Tools.Count > 0)
        {
            chatOptions.Tools = _toolRegistry.GetTools(config.ToolBelt.Tools).ToList();
        }

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "HaaSAgent",
                ChatOptions = chatOptions,
                ChatHistoryProvider = new PersistedChatHistoryProvider(_messageStore)
            });

        var session = await agent.CreateSessionAsync();
        session.StateBag.SetValue(PersistedChatHistoryProvider.SessionIdKey, sessionId);

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(config.SystemPrompt))
        {
            messages.Add(new ChatMessage(new ChatRole("system"), config.SystemPrompt));
        }
        messages.Add(new ChatMessage(ChatRole.User, signal.Payload));

        var response = await agent.RunAsync(messages, session);

        return new SessionResultValue(
            Output: response.Text,
            SessionId: sessionId);
    }
}
