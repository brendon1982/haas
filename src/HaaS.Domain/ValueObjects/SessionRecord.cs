namespace HaaS.Domain.ValueObjects;

public record SessionRecord(
    string SessionId,
    string SourceType,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
