namespace HaaS.Domain.ValueObjects;

public record SessionRecord(
    string SessionId,
    string SourceType,
    string Status,
    byte[]? AgentState,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
