namespace HaaS.Domain.ValueObjects;

public record SessionRecord(
    string SessionId,
    string SourceType,
    string Status,
    string Provider,
    string ModelId,
    string SystemPrompt,
    string Tools,
    string ThinkingLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
