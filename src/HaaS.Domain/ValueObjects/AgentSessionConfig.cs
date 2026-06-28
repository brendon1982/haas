using System.Text.Json;

namespace HaaS.Domain.ValueObjects;

public record AgentSessionConfig(
    string Provider,
    string ModelId,
    string SystemPrompt,
    IReadOnlyList<string> Tools,
    string ThinkingLevel,
    string? Endpoint = null
);
