using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public sealed class ListPolicyRulesUseCase : IListPolicyRulesUseCase
{
    private readonly IPolicyRuleRepository _repository;

    public ListPolicyRulesUseCase(IPolicyRuleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<IReadOnlyList<PolicyRule>> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);
}
