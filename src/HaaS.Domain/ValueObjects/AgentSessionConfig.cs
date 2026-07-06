namespace HaaS.Domain.ValueObjects;

public record AgentSessionConfig(
    string Provider,
    string ModelId,
    string SystemPrompt,
    ToolBelt ToolBelt,
    string ThinkingLevel,
    string? ReplyTool = null
);
