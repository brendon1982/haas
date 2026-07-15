using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Agent;

public class ToolProvider : IToolProvider
{
    private readonly ConcurrentDictionary<string, ToolDefinition> _tools = new();

    public void Register(ToolDefinition definition)
    {
        _tools[definition.Name] = definition;
    }

    public IEnumerable<ToolDefinition> GetTools(IEnumerable<string> toolNames)
    {
        return toolNames
            .Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
            .Where(t => t is not null)!;
    }
}
