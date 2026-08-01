using System.Collections.Immutable;
using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class IdentityTestBuilder
{
    private string _issuer = "test-issuer";
    private string _subject = "test-subject";
    private readonly Dictionary<string, IEnumerable<string>> _claims = new(StringComparer.Ordinal);

    private IdentityTestBuilder() { }

    public static IdentityTestBuilder Create() => new();

    public IdentityTestBuilder WithIssuer(string issuer)
    {
        _issuer = issuer;
        return this;
    }

    public IdentityTestBuilder WithSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public IdentityTestBuilder WithClaim(string type, params string[] values)
    {
        _claims[type] = values;
        return this;
    }

    public Identity Build() => new(
        _issuer,
        _subject,
        _claims.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.ToImmutableHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal));
}
