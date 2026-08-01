using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public sealed class SavePolicyRuleUseCase : ISavePolicyRuleUseCase
{
    private readonly IPolicyRuleRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SavePolicyRuleUseCase(
        IPolicyRuleRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<PolicyRule> ExecuteAsync(
        PolicyRule rule,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await _repository.GetAsync(rule.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var saved = new PolicyRule(
            rule.Id,
            rule.Gate,
            rule.Priority,
            rule.Effect,
            rule.Conditions,
            existing?.CreatedAt ?? now,
            now);
        await _repository.SaveAsync(saved, cancellationToken);
        return saved;
    }
}
