using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public interface ISavePolicyRuleUseCase
{
    Task<PolicyRule> ExecuteAsync(
        PolicyRule rule,
        CancellationToken cancellationToken = default);
}
