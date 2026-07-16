using System.Collections.Concurrent;
using System.Linq.Expressions;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Adapters.Agent;

public class ToolProvider : IToolProvider
{
    private readonly ConcurrentDictionary<string, ToolDefinition> _tools = new();
    private readonly ISignalScopeAccessor _scopeAccessor;

    public ToolProvider(ISignalScopeAccessor scopeAccessor)
    {
        _scopeAccessor = scopeAccessor;
    }

    public void Register(ToolDefinition definition)
    {
        _tools[definition.Name] = definition;
    }

    public void Register<T>(string name, string description, Expression<Func<T, Delegate>> methodSelector) where T : class
    {
        var body = methodSelector.Body;
        while (body is UnaryExpression u && (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked) && body.Type == typeof(Delegate))
        {
            body = u.Operand;
        }
        
        var delegateType = body.Type;
        var invokeMethod = delegateType.GetMethod("Invoke") ?? throw new InvalidOperationException($"Could not find Invoke method on {delegateType.Name}");
        var parameterExpressions = invokeMethod.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();

        var compiledSelector = methodSelector.Compile();

        var wrapper = Expression.Lambda(
            delegateType,
            Expression.Invoke(
                Expression.Convert(
                    Expression.Invoke(
                        Expression.Constant(compiledSelector),
                        Expression.Convert(
                            Expression.Call(
                                typeof(ServiceProviderServiceExtensions),
                                "GetRequiredService",
                                [typeof(T)],
                                Expression.Property(
                                    Expression.Constant(_scopeAccessor),
                                    nameof(ISignalScopeAccessor.ServiceProvider)
                                )
                            ),
                            typeof(T)
                        )
                    ),
                    delegateType
                ),
                parameterExpressions
            ),
            parameterExpressions
        ).Compile();

        Register(new ToolDefinition(name, description, wrapper));
    }

    public IEnumerable<ToolDefinition> GetTools(IEnumerable<string> toolNames)
    {
        return toolNames
            .Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
            .Where(t => t is not null)!;
    }
}
