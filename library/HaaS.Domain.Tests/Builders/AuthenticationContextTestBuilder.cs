using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class AuthenticationContextTestBuilder
{
    private Identity _identity = IdentityTestBuilder.Create().Build();
    private string _authenticationMethod = "test";
    private readonly List<CredentialReference> _credentialReferences = [];

    private AuthenticationContextTestBuilder() { }

    public static AuthenticationContextTestBuilder Create() => new();

    public AuthenticationContextTestBuilder WithIdentity(Identity identity)
    {
        _identity = identity;
        return this;
    }

    public AuthenticationContextTestBuilder WithAuthenticationMethod(string authenticationMethod)
    {
        _authenticationMethod = authenticationMethod;
        return this;
    }

    public AuthenticationContextTestBuilder WithCredentialReference(string name, string provider, string reference)
    {
        _credentialReferences.Add(new CredentialReference(name, provider, reference));
        return this;
    }

    public AuthenticationContext Build() => new(_identity, _authenticationMethod, _credentialReferences);
}
