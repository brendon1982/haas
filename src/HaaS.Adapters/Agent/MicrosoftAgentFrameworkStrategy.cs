using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Agent;

public class MicrosoftAgentFrameworkStrategy : IAgentStrategy
{
    private readonly IChatClientFactory _chatClientFactory;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageStore _messageStore;
    private readonly IToolProvider _toolProvider;
    private readonly TimeProvider _timeProvider;

    public MicrosoftAgentFrameworkStrategy(
        IChatClientFactory chatClientFactory,
        ISessionRepository sessionRepository,
        IMessageStore messageStore,
        IToolProvider toolProvider,
        TimeProvider timeProvider)
    {
        _chatClientFactory = chatClientFactory;
        _sessionRepository = sessionRepository;
        _messageStore = messageStore;
        _toolProvider = toolProvider;
        _timeProvider = timeProvider;
    }

    public async Task<SessionResult> ExecuteAsync(SignalValue signal, string sessionId, ISignalPresenter presenter)
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
                    [new DomainMessage("system", config.SystemPrompt, _timeProvider.GetUtcNow())]);
            }
        }

        if (!_chatClientFactory.CanCreate(config.Provider))
        {
            throw new InvalidOperationException(
                $"No chat client available for provider '{config.Provider}'.");
        }

        var chatClient = await _chatClientFactory.CreateAsync(config.Provider, config.ModelId);

        var chatOptions = new ChatOptions();
        _chatClientFactory.ConfigureOptions(config.Provider, chatOptions, config);
        if (config.ToolBelt.Tools.Count > 0)
        {
            chatOptions.Tools = _toolProvider.GetTools(config.ToolBelt.Tools)
                .Select(t => t.Method is not null
                    ? AIFunctionFactory.Create(t.Method, (Type serviceType) => _toolProvider.GetService(serviceType), new AIFunctionFactoryOptions { Name = t.Name, Description = t.Description })
                    : AIFunctionFactory.Create(t.Handler, new AIFunctionFactoryOptions { Name = t.Name, Description = t.Description }))
                .Cast<AITool>()
                .ToList();
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

        var result = new SessionResult(Output: response.Text, SessionId: sessionId);
        await presenter.PresentAsync(result);
        return result;
    }
}
