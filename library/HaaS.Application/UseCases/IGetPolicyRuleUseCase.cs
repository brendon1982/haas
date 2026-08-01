using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public interface IGetPolicyRuleUseCase
{
    Task<PolicyRule?> ExecuteAsync(string id, CancellationToken cancellationToken = default);
}
