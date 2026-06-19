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

    public MicrosoftAgentFrameworkStrategy(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<SessionResultValue> ExecuteAsync(AgentSessionConfigValue config, SignalValue signal)
    {
        var agent = new ChatClientAgent(
            _chatClient,
            name: "HaaSAgent",
            instructions: config.SystemPrompt);

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, signal.Payload)],
            session);

        return new SessionResultValue(
            Output: response.Text ?? string.Empty,
            SessionId: Guid.NewGuid().ToString());
    }
}
