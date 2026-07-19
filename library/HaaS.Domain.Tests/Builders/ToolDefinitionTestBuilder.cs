using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class ToolDefinitionTestBuilder
{
    private string _name = "default_tool";
    private string _description = "A default tool description";
    private Delegate _handler = (Func<Task>)(() => Task.CompletedTask);

    private ToolDefinitionTestBuilder() { }

    public static ToolDefinitionTestBuilder Create() => new();

    public ToolDefinitionTestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ToolDefinitionTestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ToolDefinitionTestBuilder WithHandler(Delegate handler)
    {
        _handler = handler;
        return this;
    }

    public ToolDefinition Build() => new(_name, _description, _handler);
}
