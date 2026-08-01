using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record PolicyRequest
{
    public string SessionId { get; }
    public PolicyGate Gate { get; }
    public string Source { get; }
    public Identity Identity { get; }
    public ImmutableDictionary<string, string> Attributes { get; }
    public string? CandidateToolName { get; }

    public PolicyRequest(
        string sessionId,
        PolicyGate gate,
        string source,
        Identity identity,
        IEnumerable<KeyValuePair<string, string>>? attributes = null,
        string? candidateToolName = null)
    {
        SessionId = PolicyValidation.RequireNonEmpty(sessionId, nameof(sessionId));
        PolicyValidation.RequireDefined(gate, nameof(gate));
        Source = PolicyValidation.RequireNonEmpty(source, nameof(source));
        ArgumentNullException.ThrowIfNull(identity);
        if (candidateToolName is not null)
        {
            PolicyValidation.RequireNonEmpty(candidateToolName, nameof(candidateToolName));
        }

        Gate = gate;
        Identity = identity;
        Attributes = (attributes ?? [])
            .Select(attribute => new KeyValuePair<string, string>(
                PolicyValidation.RequireNonEmpty(attribute.Key, nameof(attributes)),
                attribute.Value ?? throw new ArgumentException(
                    "Attribute values cannot be null.",
                    nameof(attributes))))
            .ToImmutableDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        CandidateToolName = candidateToolName;
    }
}
