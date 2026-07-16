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
        var delegateType = ResolveDelegateType(methodSelector);
        var parameters = CreateParameterExpressions(delegateType);
        var wrapper = BuildIoCWrapper(methodSelector, delegateType, parameters);

        Register(new ToolDefinition(name, description, wrapper));
    }

    public IEnumerable<ToolDefinition> GetTools(IEnumerable<string> toolNames)
    {
        return toolNames
            .Select(name => _tools.GetValueOrDefault(name))
            .Where(t => t is not null)!;
    }

    private Delegate BuildIoCWrapper<T>(Expression<Func<T, Delegate>> methodSelector, Type delegateType, ParameterExpression[] parameters) where T : class
    {
        var resolveInstance = Expression.Call(
            Expression.Constant(this),
            nameof(ResolveScopedService),
            [typeof(T)]
        );

        var getDelegate = Expression.Convert(
            Expression.Invoke(methodSelector, resolveInstance),
            delegateType
        );

        var callDelegate = Expression.Invoke(getDelegate, parameters);

        return Expression.Lambda(delegateType, callDelegate, parameters).Compile();
    }

    private T ResolveScopedService<T>() where T : class
    {
        var provider = _scopeAccessor.ServiceProvider
            ?? throw new InvalidOperationException("Cannot execute tool because no signal scope is active.");

        return provider.GetRequiredService<T>();
    }

    private static Type ResolveDelegateType<T>(Expression<Func<T, Delegate>> methodSelector)
    {
        var body = methodSelector.Body;
        while (body is UnaryExpression u && (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked) && body.Type == typeof(Delegate))
        {
            body = u.Operand;
        }

        return body.Type;
    }

    private static ParameterExpression[] CreateParameterExpressions(Type delegateType)
    {
        var invokeMethod = delegateType.GetMethod("Invoke")
            ?? throw new InvalidOperationException($"Could not find Invoke method on {delegateType.Name}");

        return invokeMethod.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();
    }
}
