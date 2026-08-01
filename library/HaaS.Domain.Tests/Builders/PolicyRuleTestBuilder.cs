using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class PolicyRuleTestBuilder
{
    private string _id = "rule-1";
    private PolicyGate _gate = PolicyGate.SessionStart;
    private int _priority;
    private PolicyEffect _effect = PolicyEffect.Allow;
    private PolicyConditions _conditions = PolicyConditionsTestBuilder.Create().Build();
    private DateTimeOffset _createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _updatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private PolicyRuleTestBuilder() { }

    public static PolicyRuleTestBuilder Create() => new();

    public PolicyRuleTestBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public PolicyRuleTestBuilder WithGate(PolicyGate gate)
    {
        _gate = gate;
        return this;
    }

    public PolicyRuleTestBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public PolicyRuleTestBuilder WithEffect(PolicyEffect effect)
    {
        _effect = effect;
        return this;
    }

    public PolicyRuleTestBuilder WithConditions(PolicyConditions conditions)
    {
        _conditions = conditions;
        return this;
    }

    public PolicyRuleTestBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public PolicyRuleTestBuilder WithUpdatedAt(DateTimeOffset updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public PolicyRule Build() => new(
        _id,
        _gate,
        _priority,
        _effect,
        _conditions,
        _createdAt,
        _updatedAt);
}
