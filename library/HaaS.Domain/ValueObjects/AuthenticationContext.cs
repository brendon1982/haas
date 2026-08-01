using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record AuthenticationContext
{
    public static readonly AuthenticationContext Anonymous = new(Identity.Anonymous, "anonymous");

    public Identity Identity { get; }
    public string AuthenticationMethod { get; }
    public IReadOnlyDictionary<string, CredentialReference> CredentialReferences { get; }

    public AuthenticationContext(
        Identity identity,
        string authenticationMethod,
        IEnumerable<CredentialReference>? credentialReferences = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationMethod);

        Identity = identity;
        AuthenticationMethod = authenticationMethod;
        CredentialReferences = (credentialReferences ?? [])
            .ToImmutableDictionary(reference => reference.Name, StringComparer.Ordinal);
    }
}
