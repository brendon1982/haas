using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record ClaimCondition
{
    public string ClaimType { get; }
    public ClaimMatchOperator Operator { get; }
    public ImmutableArray<string> Values { get; }

    public ClaimCondition(
        string claimType,
        ClaimMatchOperator @operator,
        IEnumerable<string>? values = null)
    {
        ClaimType = PolicyValidation.RequireNonEmpty(claimType, nameof(claimType));
        PolicyValidation.RequireDefined(@operator, nameof(@operator));
        Values = PolicyValidation.NormalizeStrings(values, nameof(values));
        PolicyValidation.ValidateValueShape(
            @operator is ClaimMatchOperator.AnyOf or ClaimMatchOperator.AllOf,
            Values,
            nameof(values));
        Operator = @operator;
    }
}
