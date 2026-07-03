using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, Func<AIFunction>> _factories = new();

    public void Register(string name, Delegate handler, string? description = null)
    {
        _factories[name] = () => AIFunctionFactory.Create(
            handler,
            new AIFunctionFactoryOptions { Name = name, Description = description });
    }

    public IReadOnlyList<AITool> GetTools(IEnumerable<string> toolNames)
    {
        return toolNames
            .Select(name => _factories.TryGetValue(name, out var factory) ? factory() : null)
            .Where(t => t is not null)
            .Cast<AITool>()
            .ToList();
    }
}
