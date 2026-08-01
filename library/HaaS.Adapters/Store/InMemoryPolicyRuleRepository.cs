using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public sealed class InMemoryPolicyRuleRepository : IPolicyRuleRepository
{
    private readonly ConcurrentDictionary<string, PolicyRule> _rules =
        new(StringComparer.Ordinal);

    public InMemoryPolicyRuleRepository(IEnumerable<PolicyRule>? seedRules = null)
    {
        foreach (var rule in seedRules ?? [])
        {
            PolicyRuleValidator.Validate(rule);
            _rules.TryAdd(rule.Id, rule);
        }
    }

    public Task<PolicyRule?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _rules.TryGetValue(id, out var rule);
        return Task.FromResult<PolicyRule?>(rule);
    }

    public Task<IReadOnlyList<PolicyRule>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<PolicyRule>>(
            _rules.Values
                .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                .ToArray());
    }

    public Task<IReadOnlyList<PolicyRule>> GetByGateAsync(
        PolicyGate gate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireDefined(gate, nameof(gate));

        return Task.FromResult<IReadOnlyList<PolicyRule>>(
            _rules.Values
                .Where(rule => rule.Gate == gate)
                .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                .ToArray());
    }

    public Task SaveAsync(PolicyRule rule, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(rule);
        PolicyRuleValidator.Validate(rule);

        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _rules.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private static void RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unsupported enum value.");
        }
    }
}
