using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record AttributeCondition
{
    public string AttributeName { get; }
    public AttributeMatchOperator Operator { get; }
    public ImmutableArray<string> Values { get; }

    public AttributeCondition(
        string attributeName,
        AttributeMatchOperator @operator,
        IEnumerable<string>? values = null)
    {
        AttributeName = PolicyValidation.RequireNonEmpty(attributeName, nameof(attributeName));
        PolicyValidation.RequireDefined(@operator, nameof(@operator));
        Values = PolicyValidation.NormalizeStrings(values, nameof(values));

        var requiresValues = @operator is AttributeMatchOperator.Equals
            or AttributeMatchOperator.NotEquals
            or AttributeMatchOperator.AnyOf;
        PolicyValidation.ValidateValueShape(requiresValues, Values, nameof(values));
        if (@operator is AttributeMatchOperator.Equals or AttributeMatchOperator.NotEquals
            && Values.Length != 1)
        {
            throw new ArgumentException(
                $"Operator '{@operator}' requires exactly one value.",
                nameof(values));
        }

        Operator = @operator;
    }
}
