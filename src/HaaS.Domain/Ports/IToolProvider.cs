using System.Linq.Expressions;
using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IToolProvider
{
    IEnumerable<ToolDefinition> GetTools(IEnumerable<string> toolNames);
    void Register(ToolDefinition definition);
    void Register<T>(string name, string description, Expression<Func<T, Delegate>> methodSelector) where T : class;
}
