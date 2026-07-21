namespace HaaS.Domain.ValueObjects;

public record IncomingSignal(
    string Payload, 
    string? SessionId = null, 
    DateTimeOffset? ArrivedAt = null,
    string? MessageId = null);
