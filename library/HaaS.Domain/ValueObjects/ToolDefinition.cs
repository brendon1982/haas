namespace HaaS.Domain.ValueObjects;

public record ToolDefinition(string Name, string Description, Delegate Handler);
