namespace HaaS.Domain.Exceptions;

public sealed class GovernanceDeniedException : Exception
{
    public string SessionId { get; }
    public string Gate { get; }
    public string ReasonCode { get; }
    public string? MatchedRuleId { get; }

    public GovernanceDeniedException(
        string sessionId,
        string gate,
        string reasonCode,
        string? matchedRuleId = null)
        : base($"Governance denied session '{sessionId}' at gate '{gate}' ({reasonCode}).")
    {
        SessionId = sessionId;
        Gate = gate;
        ReasonCode = reasonCode;
        MatchedRuleId = matchedRuleId;
    }
}
