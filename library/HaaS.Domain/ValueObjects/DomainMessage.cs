namespace HaaS.Domain.ValueObjects;

public record DomainMessage(string Role, string Content, DateTimeOffset Timestamp, string? Payload = null);
