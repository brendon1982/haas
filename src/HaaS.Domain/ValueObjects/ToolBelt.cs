namespace HaaS.Domain.ValueObjects;

public record ToolBelt(IReadOnlyList<string> Tools)
{
    public static readonly ToolBelt Empty = new([]);
}
