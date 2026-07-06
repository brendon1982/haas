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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ReplyTool = null
)
{
    public AgentSessionConfig ToConfig()
    {
        return new AgentSessionConfig(
            Provider, ModelId, SystemPrompt,
            JsonSerializer.Deserialize<ToolBelt>(Tools) ?? ToolBelt.Empty,
            ThinkingLevel,
            ReplyTool);
    }
}
