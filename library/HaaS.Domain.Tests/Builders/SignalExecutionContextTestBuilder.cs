using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class SignalExecutionContextTestBuilder
{
    private string _sessionId = "test-session";
    private string _source = "test-source";
    private AuthenticationContext _authentication = AuthenticationContextTestBuilder.Create().Build();
    private readonly Dictionary<string, string> _attributes = new(StringComparer.Ordinal);

    private SignalExecutionContextTestBuilder() { }

    public static SignalExecutionContextTestBuilder Create() => new();

    public SignalExecutionContextTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public SignalExecutionContextTestBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }

    public SignalExecutionContextTestBuilder WithAuthentication(AuthenticationContext authentication)
    {
        _authentication = authentication;
        return this;
    }

    public SignalExecutionContextTestBuilder WithAttribute(string key, string value)
    {
        _attributes[key] = value;
        return this;
    }

    public SignalExecutionContext Build() => new(_sessionId, _source, _authentication, _attributes);
}
