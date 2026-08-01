using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class PolicyRequestTestBuilder
{
    private string _sessionId = "session-1";
    private PolicyGate _gate = PolicyGate.SessionStart;
    private string _source = "test-source";
    private Identity _identity = IdentityTestBuilder.Create().Build();
    private readonly Dictionary<string, string> _attributes = new(StringComparer.Ordinal);
    private string? _candidateToolName;

    private PolicyRequestTestBuilder() { }

    public static PolicyRequestTestBuilder Create() => new();

    public PolicyRequestTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public PolicyRequestTestBuilder WithGate(PolicyGate gate)
    {
        _gate = gate;
        return this;
    }

    public PolicyRequestTestBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }

    public PolicyRequestTestBuilder WithIdentity(Identity identity)
    {
        _identity = identity;
        return this;
    }

    public PolicyRequestTestBuilder WithAttribute(string name, string value)
    {
        _attributes[name] = value;
        return this;
    }

    public PolicyRequestTestBuilder WithCandidateTool(string? toolName)
    {
        _candidateToolName = toolName;
        return this;
    }

    public PolicyRequest Build() => new(
        _sessionId,
        _gate,
        _source,
        _identity,
        _attributes,
        _candidateToolName);
}
