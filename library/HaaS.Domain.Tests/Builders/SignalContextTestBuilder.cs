using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class SignalContextTestBuilder
{
    private AuthenticationContext _authentication = AuthenticationContextTestBuilder.Create().Build();
    private readonly Dictionary<string, string> _attributes = new(StringComparer.Ordinal);

    private SignalContextTestBuilder() { }

    public static SignalContextTestBuilder Create() => new();

    public SignalContextTestBuilder WithAuthentication(AuthenticationContext authentication)
    {
        _authentication = authentication;
        return this;
    }

    public SignalContextTestBuilder WithAttribute(string key, string value)
    {
        _attributes[key] = value;
        return this;
    }

    public SignalContext Build() => new(_authentication, _attributes);
}
