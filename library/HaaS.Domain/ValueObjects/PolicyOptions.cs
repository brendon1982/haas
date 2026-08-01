namespace HaaS.Domain.ValueObjects;

public sealed record PolicyOptions
{
    public static readonly PolicyOptions Default = new();

    public PolicyEffect SessionStartFallback { get; }
    public PolicyEffect ToolResolutionFallback { get; }
    public string RoleClaimType { get; }

    public PolicyOptions(
        PolicyEffect sessionStartFallback = PolicyEffect.Allow,
        PolicyEffect toolResolutionFallback = PolicyEffect.Allow,
        string roleClaimType = "role")
    {
        PolicyValidation.RequireDefined(sessionStartFallback, nameof(sessionStartFallback));
        PolicyValidation.RequireDefined(toolResolutionFallback, nameof(toolResolutionFallback));

        SessionStartFallback = sessionStartFallback;
        ToolResolutionFallback = toolResolutionFallback;
        RoleClaimType = PolicyValidation.RequireNonEmpty(roleClaimType, nameof(roleClaimType));
    }

    public PolicyEffect GetFallback(PolicyGate gate)
    {
        PolicyValidation.RequireDefined(gate, nameof(gate));
        return gate == PolicyGate.SessionStart
            ? SessionStartFallback
            : ToolResolutionFallback;
    }
}
