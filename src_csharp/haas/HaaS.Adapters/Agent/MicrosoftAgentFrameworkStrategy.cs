using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public class MicrosoftAgentFrameworkStrategy : IAgentStrategy
{
    private readonly IChatClient _chatClient;

    public MicrosoftAgentFrameworkStrategy(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<SessionResult> ExecuteAsync(AgentSessionConfig config, Signal signal)
    {
        var agent = _chatClient.AsAIAgent(
            name: "HaaSAgent",
            instructions: config.SystemPrompt);

        var session = new AgentSession();
        var response = await agent.RunAsync(session, signal.Payload);

        return new SessionResult(
            Output: response.ToString(),
            SessionId: session.Id.ToString());
    }
}
