namespace HaaS.Domain.ValueObjects;

public record SessionResult(string Output, string SessionId, string? MessageId = null);
