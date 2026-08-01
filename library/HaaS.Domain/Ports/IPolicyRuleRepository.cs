using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IPolicyRuleRepository
{
    Task<PolicyRule?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyRule>> GetByGateAsync(
        PolicyGate gate,
        CancellationToken cancellationToken = default);
    Task SaveAsync(PolicyRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
