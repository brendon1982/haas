namespace HaaS.Domain.ValueObjects;

public record IncomingSignal(
    string Payload,
    SignalContext Context,
    string? SessionId = null,
    DateTimeOffset? ArrivedAt = null,
    string? MessageId = null);
