using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public interface IToolRegistry
{
    IReadOnlyList<AITool> GetTools(IEnumerable<string> toolNames);
    void Register(string name, Delegate handler, string? description = null);
}
