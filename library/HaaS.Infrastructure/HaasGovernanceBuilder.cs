using HaaS.Domain.ValueObjects;

namespace HaaS.Infrastructure;

public sealed class HaasGovernanceBuilder
{
    private readonly HaasGovernanceConfiguration _configuration;

    internal HaasGovernanceBuilder(HaasGovernanceConfiguration configuration)
    {
        _configuration = configuration;
    }

    public HaasGovernanceBuilder WithSessionStartFallback(PolicyEffect effect)
    {
        _configuration.SessionStartFallback = RequireDefined(effect, nameof(effect));
        return this;
    }

    public HaasGovernanceBuilder WithSessionFallback(PolicyEffect effect)
        => WithSessionStartFallback(effect);

    public HaasGovernanceBuilder WithToolResolutionFallback(PolicyEffect effect)
    {
        _configuration.ToolResolutionFallback = RequireDefined(effect, nameof(effect));
        return this;
    }

    public HaasGovernanceBuilder WithToolFallback(PolicyEffect effect)
        => WithToolResolutionFallback(effect);

    public HaasGovernanceBuilder WithRoleClaimType(string claimType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        _configuration.RoleClaimType = claimType;
        return this;
    }

    public HaasGovernanceBuilder AddRule(
        string id,
        Action<HaasPolicyRuleBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var rule = new HaasPolicyRuleBuilder(id);
        configure(rule);
        _configuration.AddSeed(rule.BuildDefinition());
        return this;
    }

    public HaasGovernanceBuilder SeedRule(
        string id,
        Action<HaasPolicyRuleBuilder> configure)
        => AddRule(id, configure);

    public HaasGovernanceBuilder AddRule(
        string id,
        PolicyGate gate,
        int priority,
        PolicyEffect effect,
        Action<HaasPolicyRuleBuilder>? configure = null)
    {
        var rule = new HaasPolicyRuleBuilder(id)
            .WithGate(gate)
            .WithPriority(priority)
            .WithEffect(effect);
        configure?.Invoke(rule);
        _configuration.AddSeed(rule.BuildDefinition());
        return this;
    }

    public HaasGovernanceBuilder SeedRule(
        string id,
        PolicyGate gate,
        int priority,
        PolicyEffect effect,
        Action<HaasPolicyRuleBuilder>? configure = null)
        => AddRule(id, gate, priority, effect, configure);

    private static PolicyEffect RequireDefined(PolicyEffect effect, string parameterName)
    {
        if (!Enum.IsDefined(effect))
        {
            throw new ArgumentOutOfRangeException(parameterName, effect, "Unsupported policy effect.");
        }

        return effect;
    }
}

public sealed class HaasPolicyRuleBuilder
{
    private readonly string _id;
    private readonly List<string> _sourceTypes = [];
    private readonly List<PolicySubject> _subjects = [];
    private readonly List<string> _roles = [];
    private readonly List<ClaimCondition> _claims = [];
    private readonly List<AttributeCondition> _attributes = [];
    private readonly List<string> _toolNames = [];
    private readonly List<UtcTimeWindow> _timeWindows = [];
    private PolicyGate? _gate;
    private PolicyEffect? _effect;
    private int _priority;

    internal HaasPolicyRuleBuilder(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _id = id;
    }

    public HaasPolicyRuleBuilder WithGate(PolicyGate gate)
    {
        if (!Enum.IsDefined(gate))
        {
            throw new ArgumentOutOfRangeException(nameof(gate), gate, "Unsupported policy gate.");
        }

        _gate = gate;
        return this;
    }

    public HaasPolicyRuleBuilder ForGate(PolicyGate gate) => WithGate(gate);

    public HaasPolicyRuleBuilder ForSessionStart() => WithGate(PolicyGate.SessionStart);

    public HaasPolicyRuleBuilder ForToolResolution() => WithGate(PolicyGate.ToolResolution);

    public HaasPolicyRuleBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public HaasPolicyRuleBuilder WithEffect(PolicyEffect effect)
    {
        if (!Enum.IsDefined(effect))
        {
            throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unsupported policy effect.");
        }

        _effect = effect;
        return this;
    }

    public HaasPolicyRuleBuilder Allow() => WithEffect(PolicyEffect.Allow);

    public HaasPolicyRuleBuilder Deny() => WithEffect(PolicyEffect.Deny);

    public HaasPolicyRuleBuilder WithSource(string sourceType)
    {
        _sourceTypes.Add(sourceType);
        return this;
    }

    public HaasPolicyRuleBuilder ForSource(string sourceType) => WithSource(sourceType);

    public HaasPolicyRuleBuilder WithSubject(string issuer, string subject)
    {
        _subjects.Add(new PolicySubject(issuer, subject));
        return this;
    }

    public HaasPolicyRuleBuilder ForSubject(string issuer, string subject)
        => WithSubject(issuer, subject);

    public HaasPolicyRuleBuilder WithRole(string role)
    {
        _roles.Add(role);
        return this;
    }

    public HaasPolicyRuleBuilder ForRole(string role) => WithRole(role);

    public HaasPolicyRuleBuilder WithClaim(
        string claimType,
        ClaimMatchOperator @operator,
        params string[] values)
    {
        _claims.Add(new ClaimCondition(claimType, @operator, values));
        return this;
    }

    public HaasPolicyRuleBuilder WithAttribute(
        string attributeName,
        AttributeMatchOperator @operator,
        params string[] values)
    {
        _attributes.Add(new AttributeCondition(attributeName, @operator, values));
        return this;
    }

    public HaasPolicyRuleBuilder WithTool(string toolName)
    {
        _toolNames.Add(toolName);
        return this;
    }

    public HaasPolicyRuleBuilder ForTool(string toolName) => WithTool(toolName);

    public HaasPolicyRuleBuilder WithUtcTimeWindow(
        IEnumerable<DayOfWeek> days,
        TimeOnly start,
        TimeOnly end)
    {
        _timeWindows.Add(new UtcTimeWindow(days, start, end));
        return this;
    }

    public HaasPolicyRuleBuilder WithTimeWindow(
        IEnumerable<DayOfWeek> days,
        TimeOnly start,
        TimeOnly end)
        => WithUtcTimeWindow(days, start, end);

    internal HaasPolicyRuleDefinition BuildDefinition()
    {
        if (_gate is null)
        {
            throw new InvalidOperationException("A policy rule gate must be configured.");
        }

        if (_effect is null)
        {
            throw new InvalidOperationException("A policy rule effect must be configured.");
        }

        var conditions = new PolicyConditions(
            _sourceTypes,
            _subjects,
            _roles,
            _claims,
            _attributes,
            _toolNames,
            _timeWindows);
        _ = new PolicyRule(
            _id,
            _gate.Value,
            _priority,
            _effect.Value,
            conditions,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        return new HaasPolicyRuleDefinition(
            _id,
            _gate.Value,
            _priority,
            _effect.Value,
            conditions);
    }
}

public static class HaasGovernanceExtensions
{
    public static HaasBuilder WithGovernance(
        this HaasBuilder builder,
        Action<HaasGovernanceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new HaasGovernanceBuilder(builder.GovernanceConfiguration));
        return builder;
    }
}

internal sealed class HaasGovernanceConfiguration
{
    private readonly List<HaasPolicyRuleDefinition> _seeds = [];

    internal PolicyEffect SessionStartFallback { get; set; } = PolicyEffect.Allow;

    internal PolicyEffect ToolResolutionFallback { get; set; } = PolicyEffect.Allow;

    internal string RoleClaimType { get; set; } = "role";

    internal PolicyOptions CreateOptions() => new(
        SessionStartFallback,
        ToolResolutionFallback,
        RoleClaimType);

    internal IReadOnlyList<PolicyRule> CreateSeedRules(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        return _seeds
            .Select(seed => seed.CreateRule(now))
            .ToArray();
    }

    internal void AddSeed(HaasPolicyRuleDefinition seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (_seeds.Any(existing => StringComparer.Ordinal.Equals(existing.Id, seed.Id)))
        {
            throw new ArgumentException(
                $"A policy seed with id '{seed.Id}' is already configured.",
                nameof(seed));
        }

        _seeds.Add(seed);
    }
}

internal sealed record HaasPolicyRuleDefinition(
    string Id,
    PolicyGate Gate,
    int Priority,
    PolicyEffect Effect,
    PolicyConditions Conditions)
{
    internal PolicyRule CreateRule(DateTimeOffset timestamp)
        => new(Id, Gate, Priority, Effect, Conditions, timestamp, timestamp);
}
