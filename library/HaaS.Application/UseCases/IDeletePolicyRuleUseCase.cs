namespace HaaS.Application.UseCases;

public interface IDeletePolicyRuleUseCase
{
    Task ExecuteAsync(string id, CancellationToken cancellationToken = default);
}
