using System.Text.Json;

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
    string? Output,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
)
{
    public static class Statuses
    {
        public const string Created = "created";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }

    public AgentSessionConfig ToConfig()
    {
        return new AgentSessionConfig(
            Provider, ModelId, SystemPrompt,
            JsonSerializer.Deserialize<ToolBelt>(Tools) ?? ToolBelt.Empty,
            ThinkingLevel);
    }
}
