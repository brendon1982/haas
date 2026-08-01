using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class PolicyConditionsTestBuilder
{
    private readonly List<string> _sourceTypes = [];
    private readonly List<PolicySubject> _subjects = [];
    private readonly List<string> _roles = [];
    private readonly List<ClaimCondition> _claims = [];
    private readonly List<AttributeCondition> _attributes = [];
    private readonly List<string> _toolNames = [];
    private readonly List<UtcTimeWindow> _timeWindows = [];

    private PolicyConditionsTestBuilder() { }

    public static PolicyConditionsTestBuilder Create() => new();

    public PolicyConditionsTestBuilder WithSource(string source)
    {
        _sourceTypes.Add(source);
        return this;
    }

    public PolicyConditionsTestBuilder WithSubject(string issuer, string subject)
    {
        _subjects.Add(new PolicySubject(issuer, subject));
        return this;
    }

    public PolicyConditionsTestBuilder WithRole(string role)
    {
        _roles.Add(role);
        return this;
    }

    public PolicyConditionsTestBuilder WithClaim(
        string claimType,
        ClaimMatchOperator @operator,
        params string[] values)
    {
        _claims.Add(new ClaimCondition(claimType, @operator, values));
        return this;
    }

    public PolicyConditionsTestBuilder WithAttribute(
        string attributeName,
        AttributeMatchOperator @operator,
        params string[] values)
    {
        _attributes.Add(new AttributeCondition(attributeName, @operator, values));
        return this;
    }

    public PolicyConditionsTestBuilder WithTool(string toolName)
    {
        _toolNames.Add(toolName);
        return this;
    }

    public PolicyConditionsTestBuilder WithUtcTimeWindow(
        IEnumerable<DayOfWeek> days,
        TimeOnly start,
        TimeOnly end)
    {
        _timeWindows.Add(new UtcTimeWindow(days, start, end));
        return this;
    }

    public PolicyConditions Build() => new(
        _sourceTypes,
        _subjects,
        _roles,
        _claims,
        _attributes,
        _toolNames,
        _timeWindows);
}
