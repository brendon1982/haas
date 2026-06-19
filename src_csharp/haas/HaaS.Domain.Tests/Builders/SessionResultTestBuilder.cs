using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class SessionResultTestBuilder
{
    private string _output = "default output";
    private string _sessionId = "sess-default";

    private SessionResultTestBuilder() { }

    public static SessionResultTestBuilder Create() => new();

    public SessionResultTestBuilder WithOutput(string output)
    {
        _output = output;
        return this;
    }

    public SessionResultTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public SessionResult Build() => new(_output, _sessionId);
}
