namespace HaaS.Domain.ValueObjects;

public sealed record PolicyRule
{
    public string Id { get; }
    public PolicyGate Gate { get; }
    public int Priority { get; }
    public PolicyEffect Effect { get; }
    public PolicyConditions Conditions { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public PolicyRule(
        string id,
        PolicyGate gate,
        int priority,
        PolicyEffect effect,
        PolicyConditions conditions,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = PolicyValidation.RequireNonEmpty(id, nameof(id));
        PolicyValidation.RequireDefined(gate, nameof(gate));
        PolicyValidation.RequireDefined(effect, nameof(effect));
        ArgumentNullException.ThrowIfNull(conditions);

        Gate = gate;
        Priority = priority;
        Effect = effect;
        Conditions = conditions;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = updatedAt.ToUniversalTime();

        PolicyRuleValidator.Validate(this);
    }
}
