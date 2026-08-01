using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public sealed class GetPolicyRuleUseCase : IGetPolicyRuleUseCase
{
    private readonly IPolicyRuleRepository _repository;

    public GetPolicyRuleUseCase(IPolicyRuleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<PolicyRule?> ExecuteAsync(string id, CancellationToken cancellationToken = default)
        => _repository.GetAsync(id, cancellationToken);
}
