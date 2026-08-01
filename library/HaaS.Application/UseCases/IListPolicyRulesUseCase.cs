using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public interface IListPolicyRulesUseCase
{
    Task<IReadOnlyList<PolicyRule>> ExecuteAsync(CancellationToken cancellationToken = default);
}
