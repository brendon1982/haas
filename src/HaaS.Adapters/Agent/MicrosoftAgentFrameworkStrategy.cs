using System.Text.Json;
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

    public MicrosoftAgentFrameworkStrategy(IChatClient chatClient)
        : this(chatClient, new InMemorySessionRepository())
    {
    }

    public MicrosoftAgentFrameworkStrategy(IChatClient chatClient, ISessionRepository sessionRepository)
    {
        _chatClient = chatClient;
        _sessionRepository = sessionRepository;
        _agent = new ChatClientAgent(
            _chatClient,
            name: "HaaSAgent");
    }

    public async Task<SessionResultValue> ExecuteAsync(AgentSessionConfigValue config, SignalValue signal)
    {
        AgentSession session;
        string sessionId;

        if (signal.SessionId is not null)
        {
            var existing = await _sessionRepository.LoadAsync(signal.SessionId);
            if (existing?.AgentState is not null)
            {
                try
                {
                    session = await DeserializeSessionAsync(existing.AgentState);
                    sessionId = existing.SessionId;
                }
                catch (JsonException)
                {
                    session = await _agent.CreateSessionAsync();
                    sessionId = Guid.NewGuid().ToString();
                }
            }
            else
            {
                session = await _agent.CreateSessionAsync();
                sessionId = Guid.NewGuid().ToString();
            }
        }
        else
        {
            session = await _agent.CreateSessionAsync();
            sessionId = Guid.NewGuid().ToString();
        }

        var response = await _agent.RunAsync(
            [new ChatMessage(ChatRole.User, signal.Payload)],
            session);

        var serialized = await SerializeSessionAsync(session);
        var record = new HaaS.Domain.ValueObjects.SessionRecord(
            sessionId,
            signal.Source,
            "running",
            serialized,
            DateTime.UtcNow,
            DateTime.UtcNow);
        await _sessionRepository.SaveAsync(record);

        return new SessionResultValue(
            Output: response.Text ?? string.Empty,
            SessionId: sessionId);
    }

    private async Task<byte[]> SerializeSessionAsync(AgentSession session)
    {
        var element = await _agent.SerializeSessionAsync(session);
        return JsonSerializer.SerializeToUtf8Bytes(element);
    }

    private async Task<AgentSession> DeserializeSessionAsync(byte[] state)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(state);
        return await _agent.DeserializeSessionAsync(element);
    }
}
