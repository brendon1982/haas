using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class AgentExecutionRequestTestBuilder
{
    private Signal _signal = SignalTestBuilder.Create().Build();
    private string _sessionId = "test-session";
    private readonly List<string> _permittedToolNames = [];

    private AgentExecutionRequestTestBuilder() { }

    public static AgentExecutionRequestTestBuilder Create() => new();

    public AgentExecutionRequestTestBuilder WithSignal(Signal signal)
    {
        _signal = signal;
        return this;
    }

    public AgentExecutionRequestTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public AgentExecutionRequestTestBuilder WithPermittedTool(string toolName)
    {
        _permittedToolNames.Add(toolName);
        return this;
    }

    public AgentExecutionRequest Build() => new(_signal, _sessionId, _permittedToolNames);
}
