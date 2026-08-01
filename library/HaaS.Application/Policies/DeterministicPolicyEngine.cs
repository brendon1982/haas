using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.Policies;

public sealed class DeterministicPolicyEngine : IPolicyEngine
{
    private readonly IPolicyRuleRepository _repository;
    private readonly PolicyOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public DeterministicPolicyEngine(
        IPolicyRuleRepository repository,
        PolicyOptions options,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PolicyDecision> EvaluateAsync(
        PolicyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var now = _timeProvider.GetUtcNow();
        var rules = await _repository.GetByGateAsync(request.Gate, cancellationToken);
        var matchingRules = new List<MatchedRule>();
        foreach (var rule in rules)
        {
            PolicyRuleValidator.Validate(rule);
            if (rule.Gate != request.Gate)
            {
                continue;
            }

            if (TryMatch(rule, request, now, out var categories))
            {
                matchingRules.Add(new MatchedRule(rule, categories));
            }
        }

        var decision = CreateDecision(request.Gate, matchingRules);
        LogDecision(request, decision);
        return decision;
    }

    private PolicyDecision CreateDecision(
        PolicyGate gate,
        IReadOnlyCollection<MatchedRule> matchingRules)
    {
        if (matchingRules.Count == 0)
        {
            return new PolicyDecision(
                _options.GetFallback(gate),
                null,
                PolicyDecisionReasonCodes.Fallback,
                null);
        }

        var highestPriority = matchingRules.Max(match => match.Rule.Priority);
        var selected = matchingRules
            .Where(match => match.Rule.Priority == highestPriority)
            .OrderByDescending(match => match.Rule.Effect == PolicyEffect.Deny)
            .ThenBy(match => match.Rule.Id, StringComparer.Ordinal)
            .First();

        return new PolicyDecision(
            selected.Rule.Effect,
            selected.Rule.Id,
            PolicyDecisionReasonCodes.RuleMatch,
            selected.Rule.Priority,
            selected.Categories);
    }

    private bool TryMatch(
        PolicyRule rule,
        PolicyRequest request,
        DateTimeOffset now,
        out IReadOnlyList<string> categories)
    {
        var matchedCategories = new List<string>();
        var conditions = rule.Conditions;

        if (!conditions.SourceTypes.IsEmpty)
        {
            if (!conditions.SourceTypes.Contains(request.Source, StringComparer.Ordinal))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.Source);
        }

        if (!conditions.Subjects.IsEmpty)
        {
            if (!conditions.Subjects.Any(subject =>
                    StringComparer.Ordinal.Equals(subject.Issuer, request.Identity.Issuer)
                    && StringComparer.Ordinal.Equals(subject.Subject, request.Identity.Subject)))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.Subject);
        }

        if (!conditions.Roles.IsEmpty)
        {
            var roles = request.Identity.GetClaimValues(_options.RoleClaimType);
            if (!conditions.Roles.Any(roles.Contains))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.Role);
        }

        if (!conditions.Claims.IsEmpty)
        {
            if (!conditions.Claims.All(condition => MatchesClaim(condition, request.Identity)))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.Claim);
        }

        if (!conditions.Attributes.IsEmpty)
        {
            if (!conditions.Attributes.All(condition => MatchesAttribute(condition, request.Attributes)))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.Attribute);
        }

        if (!conditions.ToolNames.IsEmpty)
        {
            if (request.CandidateToolName is null
                || !conditions.ToolNames.Contains(request.CandidateToolName, StringComparer.Ordinal))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.Tool);
        }

        if (!conditions.TimeWindows.IsEmpty)
        {
            if (!conditions.TimeWindows.Any(window => MatchesUtcWindow(window, now)))
            {
                categories = [];
                return false;
            }

            matchedCategories.Add(PolicyConditionCategories.TimeWindow);
        }

        categories = matchedCategories;
        return true;
    }

    private static bool MatchesClaim(ClaimCondition condition, Identity identity)
    {
        return condition.Operator switch
        {
            ClaimMatchOperator.Exists => identity.HasClaimType(condition.ClaimType),
            ClaimMatchOperator.Absent => !identity.HasClaimType(condition.ClaimType),
            ClaimMatchOperator.AnyOf => condition.Values.Any(value =>
                identity.HasClaim(condition.ClaimType, value)),
            ClaimMatchOperator.AllOf => condition.Values.All(value =>
                identity.HasClaim(condition.ClaimType, value)),
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };
    }

    private static bool MatchesAttribute(
        AttributeCondition condition,
        IReadOnlyDictionary<string, string> attributes)
    {
        var hasAttribute = attributes.TryGetValue(condition.AttributeName, out var value);

        return condition.Operator switch
        {
            AttributeMatchOperator.Exists => hasAttribute,
            AttributeMatchOperator.Absent => !hasAttribute,
            AttributeMatchOperator.Equals => hasAttribute
                && StringComparer.Ordinal.Equals(value, condition.Values[0]),
            AttributeMatchOperator.NotEquals => hasAttribute
                && !StringComparer.Ordinal.Equals(value, condition.Values[0]),
            AttributeMatchOperator.AnyOf => hasAttribute
                && condition.Values.Contains(value!, StringComparer.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };
    }

    private static bool MatchesUtcWindow(UtcTimeWindow window, DateTimeOffset now)
    {
        var utcNow = now.ToUniversalTime();
        var currentDay = utcNow.DayOfWeek;
        var currentTime = TimeOnly.FromDateTime(utcNow.UtcDateTime);

        if (window.Start < window.End)
        {
            return window.Days.Contains(currentDay)
                && currentTime >= window.Start
                && currentTime < window.End;
        }

        if (currentTime >= window.Start)
        {
            return window.Days.Contains(currentDay);
        }

        if (currentTime < window.End)
        {
            var startDay = currentDay == DayOfWeek.Sunday
                ? DayOfWeek.Saturday
                : currentDay - 1;
            return window.Days.Contains(startDay);
        }

        return false;
    }

    private void LogDecision(PolicyRequest request, PolicyDecision decision)
    {
        _logger.LogInformation(
            "Policy decision Gate={0} Allowed={1} Effect={2} RuleId={3} Reason={4} Priority={5} Conditions={6} Source={7} Tool={8} Issuer={9} Subject={10}",
            request.Gate,
            decision.Allowed,
            decision.Effect,
            decision.MatchedRuleId,
            decision.ReasonCode,
            decision.Priority,
            string.Join(",", decision.MatchedConditionCategories),
            request.Source,
            request.CandidateToolName,
            request.Identity.Issuer,
            request.Identity.Subject);
    }

    private sealed record MatchedRule(PolicyRule Rule, IReadOnlyList<string> Categories);
}
