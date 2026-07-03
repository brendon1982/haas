namespace HaaS.Domain.ValueObjects;

public record SignalSourceConfig(
    string SourceType,
    string Provider,
    string ModelId,
    string SystemPrompt,
    ToolBelt ToolBelt,
    string ThinkingLevel
)
{
    public AgentSessionConfig ToSessionConfig() => new(
        Provider, ModelId, SystemPrompt, ToolBelt, ThinkingLevel);
}
