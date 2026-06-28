using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SignalValue = HaaS.Domain.ValueObjects.Signal;
using SessionResultValue = HaaS.Domain.ValueObjects.SessionResult;
using AgentSessionConfigValue = HaaS.Domain.ValueObjects.AgentSessionConfig;

namespace HaaS.Adapters.Agent;

public class MicrosoftAgentFrameworkStrategy : IAgentStrategy
{
    private readonly IChatClientFactory _chatClientFactory;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageStore _messageStore;

    public MicrosoftAgentFrameworkStrategy(
        IChatClientFactory chatClientFactory,
        ISessionRepository sessionRepository,
        IMessageStore messageStore)
    {
        _chatClientFactory = chatClientFactory;
        _sessionRepository = sessionRepository;
        _messageStore = messageStore;
    }

    public async Task<SessionResultValue> ExecuteAsync(SignalValue signal, string sessionId)
    {
        var record = await _sessionRepository.LoadAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var config = new AgentSessionConfigValue(
            record.Provider,
            record.ModelId,
            record.SystemPrompt,
            JsonSerializer.Deserialize<IReadOnlyList<string>>(record.Tools) ?? [],
            record.ThinkingLevel);

        var chatClient = _chatClientFactory.Create(config);
        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "HaaSAgent",
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
