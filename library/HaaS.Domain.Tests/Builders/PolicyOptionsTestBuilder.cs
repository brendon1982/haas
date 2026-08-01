using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class PolicyOptionsTestBuilder
{
    private PolicyEffect _sessionStartFallback = PolicyEffect.Allow;
    private PolicyEffect _toolResolutionFallback = PolicyEffect.Allow;
    private string _roleClaimType = "role";

    private PolicyOptionsTestBuilder() { }

    public static PolicyOptionsTestBuilder Create() => new();

    public PolicyOptionsTestBuilder WithSessionStartFallback(PolicyEffect effect)
    {
        _sessionStartFallback = effect;
        return this;
    }

    public PolicyOptionsTestBuilder WithToolResolutionFallback(PolicyEffect effect)
    {
        _toolResolutionFallback = effect;
        return this;
    }

    public PolicyOptionsTestBuilder WithRoleClaimType(string claimType)
    {
        _roleClaimType = claimType;
        return this;
    }

    public PolicyOptions Build() => new(
        _sessionStartFallback,
        _toolResolutionFallback,
        _roleClaimType);
}
