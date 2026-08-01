using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record SignalExecutionContext
{
    public string SessionId { get; }
    public string Source { get; }
    public AuthenticationContext Authentication { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }

    public SignalExecutionContext(
        string sessionId,
        string source,
        AuthenticationContext authentication,
        IEnumerable<KeyValuePair<string, string>>? attributes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(authentication);

        SessionId = sessionId;
        Source = source;
        Authentication = authentication;
        Attributes = (attributes ?? [])
            .ToImmutableDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
    }
}
