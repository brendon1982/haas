using HaaS.Domain.Ports;

namespace HaaS.Application.UseCases;

public sealed class DeletePolicyRuleUseCase : IDeletePolicyRuleUseCase
{
    private readonly IPolicyRuleRepository _repository;

    public DeletePolicyRuleUseCase(IPolicyRuleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task ExecuteAsync(string id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);
}
