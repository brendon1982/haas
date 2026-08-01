using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record PolicyDecision
{
    public bool Allowed => Effect == PolicyEffect.Allow;
    public PolicyEffect Effect { get; }
    public string? MatchedRuleId { get; }
    public string ReasonCode { get; }
    public int? Priority { get; }
    public ImmutableArray<string> MatchedConditionCategories { get; }

    public PolicyDecision(
        PolicyEffect effect,
        string? matchedRuleId,
        string reasonCode,
        int? priority,
        IEnumerable<string>? matchedConditionCategories = null)
    {
        PolicyValidation.RequireDefined(effect, nameof(effect));
        if (matchedRuleId is not null)
        {
            PolicyValidation.RequireNonEmpty(matchedRuleId, nameof(matchedRuleId));
        }

        Effect = effect;
        MatchedRuleId = matchedRuleId;
        ReasonCode = PolicyValidation.RequireNonEmpty(reasonCode, nameof(reasonCode));
        Priority = priority;
        MatchedConditionCategories = PolicyValidation.NormalizeStrings(
            matchedConditionCategories,
            nameof(matchedConditionCategories));
    }
}
