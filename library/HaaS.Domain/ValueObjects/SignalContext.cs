using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record SignalContext
{
    public static readonly SignalContext Anonymous = new(AuthenticationContext.Anonymous);

    public AuthenticationContext Authentication { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }

    public SignalContext(
        AuthenticationContext authentication,
        IEnumerable<KeyValuePair<string, string>>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        Authentication = authentication;
        Attributes = (attributes ?? [])
            .ToImmutableDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
    }
}
