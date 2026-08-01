using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluateAsync(
        PolicyRequest request,
        CancellationToken cancellationToken);
}
