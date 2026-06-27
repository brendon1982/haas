using HaaS.Adapters.Persistence;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SignalValue = HaaS.Domain.ValueObjects.Signal;
using SessionResultValue = HaaS.Domain.ValueObjects.SessionResult;
using AgentSessionConfigValue = HaaS.Domain.ValueObjects.AgentSessionConfig;

namespace HaaS.Adapters.Agent;

public class MicrosoftAgentFrameworkStrategy : IAgentStrategy
{
    private readonly IChatClient _chatClient;
    private readonly ISessionRepository _sessionRepository;
    private readonly ChatClientAgent _agent;
    private readonly ISessionMessageStore _messageStore;

    public MicrosoftAgentFrameworkStrategy(IChatClient chatClient)
        : this(chatClient, new InMemorySessionRepository(), new InMemorySessionMessageStore())
    {
    }

    public MicrosoftAgentFrameworkStrategy(IChatClient chatClient, ISessionRepository sessionRepository)
        : this(chatClient, sessionRepository, new InMemorySessionMessageStore())
    {
    }

    public MicrosoftAgentFrameworkStrategy(
        IChatClient chatClient,
        ISessionRepository sessionRepository,
        ISessionMessageStore messageStore)
    {
        _chatClient = chatClient;
        _sessionRepository = sessionRepository;
        _messageStore = messageStore;
        _agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = "HaaSAgent",
                ChatHistoryProvider = new PersistedChatHistoryProvider(_messageStore)
            });
    }

    public async Task<SessionResultValue> ExecuteAsync(AgentSessionConfigValue config, SignalValue signal)
    {
        string sessionId;
        if (signal.SessionId is not null)
        {
            var existing = await _sessionRepository.LoadAsync(signal.SessionId);
            sessionId = existing?.SessionId ?? Guid.NewGuid().ToString();
        }
        else
        {
            sessionId = Guid.NewGuid().ToString();
        }

        var session = await _agent.CreateSessionAsync();
        session.StateBag.SetValue(PersistedChatHistoryProvider.SessionIdKey, sessionId);

        var response = await _agent.RunAsync(
            [new ChatMessage(ChatRole.User, signal.Payload)],
            session);

        var record = new HaaS.Domain.ValueObjects.SessionRecord(
            sessionId,
            signal.Source,
            "running",
            DateTime.UtcNow,
            DateTime.UtcNow);
        await _sessionRepository.SaveAsync(record);

        return new SessionResultValue(
            Output: response.Text ?? string.Empty,
            SessionId: sessionId);
    }
}
