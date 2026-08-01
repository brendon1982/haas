using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record AgentExecutionRequest
{
    public Signal Signal { get; }
    public string SessionId { get; }
    public IReadOnlySet<string> PermittedToolNames { get; }

    public AgentExecutionRequest(
        Signal signal,
        string sessionId,
        IEnumerable<string>? permittedToolNames = null)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        Signal = signal;
        SessionId = sessionId;
        PermittedToolNames = (permittedToolNames ?? [])
            .ToImmutableHashSet(StringComparer.Ordinal);
    }
}
