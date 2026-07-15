using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IToolProvider
{
    IEnumerable<ToolDefinition> GetTools(IEnumerable<string> toolNames);
    void Register(ToolDefinition definition);
}
