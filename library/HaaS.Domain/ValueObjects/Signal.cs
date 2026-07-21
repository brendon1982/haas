namespace HaaS.Domain.ValueObjects;

public record Signal(
    string Payload, 
    string Source, 
    string? SessionId = null, 
    DateTimeOffset? ArrivedAt = null,
    string? MessageId = null);
