using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class SignalTestBuilder
{
    private string _payload = "default prompt";
    private string _source = "test";
    private string? _sessionId;

    private SignalTestBuilder() { }

    public static SignalTestBuilder Create() => new();

    public SignalTestBuilder WithPayload(string payload)
    {
        _payload = payload;
        return this;
    }

    public SignalTestBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }

    public SignalTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public Signal Build() => new(_payload, _source, _sessionId);
}
