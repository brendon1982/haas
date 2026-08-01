using HaaS.Application.Policies;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Application.Tests;

[TestFixture]
public class DeterministicPolicyEngineTests
{
    [Test]
    public async Task EvaluateAsync_ShouldApplySessionFallbackWhenNoRuleMatches()
    {
        // Arrange
        var fallback = PolicyEffect.Deny;
        var options = PolicyOptionsTestBuilder.Create()
            .WithSessionStartFallback(fallback)
            .Build();
        var request = PolicyRequestTestBuilder.Create().Build();
        var sut = SutBuilder.Create()
            .WithOptions(options)
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.False();
        Expect(decision.Effect).To.Equal(fallback);
        Expect(decision.MatchedRuleId).To.Be.Null();
        Expect(decision.ReasonCode).To.Equal(PolicyDecisionReasonCodes.Fallback);
        Expect(decision.Priority).To.Be.Null();
    }

    [Test]
    public async Task EvaluateAsync_ShouldApplyToolFallbackWhenNoRuleMatches()
    {
        // Arrange
        var fallback = PolicyEffect.Deny;
        var options = PolicyOptionsTestBuilder.Create()
            .WithToolResolutionFallback(fallback)
            .Build();
        var request = PolicyRequestTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithCandidateTool("search")
            .Build();
        var sut = SutBuilder.Create()
            .WithOptions(options)
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.False();
        Expect(decision.Effect).To.Equal(fallback);
        Expect(decision.ReasonCode).To.Equal(PolicyDecisionReasonCodes.Fallback);
    }

    [Test]
    public async Task EvaluateAsync_ShouldMatchAnEmptyRuleForItsGate()
    {
        // Arrange
        var ruleId = "allow-all-session-starts";
        var priority = 11;
        var rule = PolicyRuleTestBuilder.Create()
            .WithId(ruleId)
            .WithPriority(priority)
            .Build();
        var request = PolicyRequestTestBuilder.Create().Build();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.Effect).To.Equal(PolicyEffect.Allow);
        Expect(decision.MatchedRuleId).To.Equal(ruleId);
        Expect(decision.Priority).To.Equal(priority);
        Expect(decision.MatchedConditionCategories.AsEnumerable()).To.Be.Empty();
    }

    [Test]
    public async Task EvaluateAsync_ShouldSelectTheHigherPriorityRule()
    {
        // Arrange
        var winningRuleId = "higher-allow";
        var lowerPriority = 3;
        var higherPriority = 4;
        var lowerRule = PolicyRuleTestBuilder.Create()
            .WithId("lower-deny")
            .WithPriority(lowerPriority)
            .WithEffect(PolicyEffect.Deny)
            .Build();
        var higherRule = PolicyRuleTestBuilder.Create()
            .WithId(winningRuleId)
            .WithPriority(higherPriority)
            .Build();
        var request = PolicyRequestTestBuilder.Create().Build();
        var sut = SutBuilder.Create()
            .WithRules([lowerRule, higherRule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedRuleId).To.Equal(winningRuleId);
        Expect(decision.Priority).To.Equal(higherPriority);
    }

    [Test]
    public async Task EvaluateAsync_ShouldPreferDenyWhenHighestPriorityRulesTie()
    {
        // Arrange
        var denyRuleId = "same-priority-deny";
        var priority = 5;
        var allowRule = PolicyRuleTestBuilder.Create()
            .WithId("same-priority-allow")
            .WithPriority(priority)
            .Build();
        var denyRule = PolicyRuleTestBuilder.Create()
            .WithId(denyRuleId)
            .WithPriority(priority)
            .WithEffect(PolicyEffect.Deny)
            .Build();
        var request = PolicyRequestTestBuilder.Create().Build();
        var sut = SutBuilder.Create()
            .WithRules([allowRule, denyRule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.False();
        Expect(decision.Effect).To.Equal(PolicyEffect.Deny);
        Expect(decision.MatchedRuleId).To.Equal(denyRuleId);
    }

    [Test]
    public async Task EvaluateAsync_ShouldRequireAllCategoriesAndAllowAlternativesWithinEachCategory()
    {
        // Arrange
        var matchingSource = "slack";
        var matchingRole = "approver";
        var matchingAttribute = "fabrikam";
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithSource("webhook")
            .WithSource(matchingSource)
            .WithRole("operator")
            .WithRole(matchingRole)
            .WithAttribute("tenant", AttributeMatchOperator.AnyOf, "contoso", matchingAttribute)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var identity = IdentityTestBuilder.Create()
            .WithClaim("role", matchingRole)
            .Build();
        var matchingRequest = PolicyRequestTestBuilder.Create()
            .WithSource(matchingSource)
            .WithIdentity(identity)
            .WithAttribute("tenant", matchingAttribute)
            .Build();
        var nonMatchingRequest = PolicyRequestTestBuilder.Create()
            .WithSource(matchingSource)
            .WithIdentity(identity)
            .WithAttribute("tenant", "other")
            .Build();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .Build();

        // Act
        var matchingDecision = await sut.EvaluateAsync(matchingRequest, CancellationToken.None);
        var nonMatchingDecision = await sut.EvaluateAsync(nonMatchingRequest, CancellationToken.None);

        // Assert
        Expect(matchingDecision.Allowed).To.Be.True();
        Expect(matchingDecision.MatchedConditionCategories.AsEnumerable()).To.Contain.All.Of(
            PolicyConditionCategories.Source,
            PolicyConditionCategories.Role,
            PolicyConditionCategories.Attribute);
        Expect(nonMatchingDecision.MatchedRuleId).To.Be.Null();
    }

    [Test]
    public async Task EvaluateAsync_ShouldMatchSourceAndStableSubjectOrdinally()
    {
        // Arrange
        var source = "webhook";
        var issuer = "https://issuer.example";
        var subject = "subject-42";
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithSource(source)
            .WithSubject("other-issuer", "other-subject")
            .WithSubject(issuer, subject)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var identity = IdentityTestBuilder.Create()
            .WithIssuer(issuer)
            .WithSubject(subject)
            .Build();
        var request = PolicyRequestTestBuilder.Create()
            .WithSource(source)
            .WithIdentity(identity)
            .Build();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedConditionCategories.AsEnumerable()).To.Contain.All.Of(
            PolicyConditionCategories.Source,
            PolicyConditionCategories.Subject);
    }

    [Test]
    public async Task EvaluateAsync_ShouldReadRolesFromTheConfiguredClaimType()
    {
        // Arrange
        var roleClaimType = "groups";
        var requiredRole = "finance";
        var options = PolicyOptionsTestBuilder.Create()
            .WithRoleClaimType(roleClaimType)
            .Build();
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithRole(requiredRole)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var identity = IdentityTestBuilder.Create()
            .WithClaim(roleClaimType, requiredRole)
            .WithClaim("role", "unrelated")
            .Build();
        var request = PolicyRequestTestBuilder.Create()
            .WithIdentity(identity)
            .Build();
        var sut = SutBuilder.Create()
            .WithOptions(options)
            .WithRules([rule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedConditionCategories.AsEnumerable()).To.Contain(PolicyConditionCategories.Role);
    }

    [TestCase(ClaimMatchOperator.Exists)]
    [TestCase(ClaimMatchOperator.Absent)]
    [TestCase(ClaimMatchOperator.AnyOf)]
    [TestCase(ClaimMatchOperator.AllOf)]
    public async Task EvaluateAsync_ShouldApplyEachClaimOperator(ClaimMatchOperator @operator)
    {
        // Arrange
        var claimType = "department";
        var matchingValue = "engineering";
        var additionalValue = "security";
        var values = @operator switch
        {
            ClaimMatchOperator.AnyOf => new[] { "other", matchingValue },
            ClaimMatchOperator.AllOf => new[] { matchingValue, additionalValue },
            _ => []
        };
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithClaim(claimType, @operator, values)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var identityBuilder = IdentityTestBuilder.Create();
        if (@operator != ClaimMatchOperator.Absent)
        {
            identityBuilder.WithClaim(claimType, matchingValue, additionalValue);
        }

        var request = PolicyRequestTestBuilder.Create()
            .WithIdentity(identityBuilder.Build())
            .Build();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedConditionCategories.AsEnumerable()).To.Contain(PolicyConditionCategories.Claim);
    }

    [TestCase(AttributeMatchOperator.Exists)]
    [TestCase(AttributeMatchOperator.Absent)]
    [TestCase(AttributeMatchOperator.Equals)]
    [TestCase(AttributeMatchOperator.NotEquals)]
    [TestCase(AttributeMatchOperator.AnyOf)]
    public async Task EvaluateAsync_ShouldApplyEachAttributeOperator(AttributeMatchOperator @operator)
    {
        // Arrange
        var attributeName = "tenant";
        var matchingValue = "contoso";
        var alternateValue = "fabrikam";
        var values = @operator switch
        {
            AttributeMatchOperator.Equals => new[] { matchingValue },
            AttributeMatchOperator.NotEquals => new[] { alternateValue },
            AttributeMatchOperator.AnyOf => new[] { "other", matchingValue },
            _ => []
        };
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithAttribute(attributeName, @operator, values)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var requestBuilder = PolicyRequestTestBuilder.Create();
        if (@operator != AttributeMatchOperator.Absent)
        {
            requestBuilder.WithAttribute(attributeName, matchingValue);
        }

        var request = requestBuilder.Build();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedConditionCategories.AsEnumerable()).To.Contain(PolicyConditionCategories.Attribute);
    }

    [Test]
    public async Task EvaluateAsync_ShouldApplyToolRulesOnlyToTheirCandidateAndToolRulesWithoutToolsToAllCandidates()
    {
        // Arrange
        var matchingTool = "search";
        var unmatchedTool = "deploy";
        var toolConditions = PolicyConditionsTestBuilder.Create()
            .WithTool("read")
            .WithTool(matchingTool)
            .Build();
        var targetedRule = PolicyRuleTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithId("targeted-rule")
            .WithConditions(toolConditions)
            .Build();
        var allToolsRule = PolicyRuleTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithId("all-tools-rule")
            .WithPriority(-1)
            .WithConditions(PolicyConditionsTestBuilder.Create().WithSource("not-this-source").Build())
            .Build();
        var matchingRequest = PolicyRequestTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithCandidateTool(matchingTool)
            .Build();
        var unmatchedRequest = PolicyRequestTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithCandidateTool(unmatchedTool)
            .Build();
        var sut = SutBuilder.Create()
            .WithRules([targetedRule, allToolsRule])
            .Build();

        // Act
        var matchingDecision = await sut.EvaluateAsync(matchingRequest, CancellationToken.None);
        var unmatchedDecision = await sut.EvaluateAsync(unmatchedRequest, CancellationToken.None);

        // Assert
        Expect(matchingDecision.Allowed).To.Be.True();
        Expect(matchingDecision.MatchedConditionCategories.AsEnumerable()).To.Contain(PolicyConditionCategories.Tool);
        Expect(unmatchedDecision.MatchedRuleId).To.Be.Null();
    }

    [Test]
    public async Task EvaluateAsync_ShouldApplyNoToolConditionToEveryCandidateTool()
    {
        // Arrange
        var candidateTool = "deploy";
        var ruleId = "all-tools";
        var rule = PolicyRuleTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithId(ruleId)
            .Build();
        var request = PolicyRequestTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithCandidateTool(candidateTool)
            .Build();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedRuleId).To.Equal(ruleId);
    }

    [Test]
    public async Task EvaluateAsync_ShouldMatchOrdinaryUtcWindowsAndExcludeTheirEnd()
    {
        // Arrange
        var day = DayOfWeek.Monday;
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithUtcTimeWindow([day], new TimeOnly(7, 0), new TimeOnly(8, 0))
            .WithUtcTimeWindow([day], start, end)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var insideRequest = PolicyRequestTestBuilder.Create().Build();
        var endRequest = PolicyRequestTestBuilder.Create().Build();
        var insideTime = new DateTimeOffset(2026, 6, 29, 16, 59, 59, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2026, 6, 29, 17, 0, 0, TimeSpan.Zero);
        var insideSut = SutBuilder.Create()
            .WithRules([rule])
            .WithTime(insideTime)
            .Build();
        var endSut = SutBuilder.Create()
            .WithRules([rule])
            .WithTime(endTime)
            .Build();

        // Act
        var insideDecision = await insideSut.EvaluateAsync(insideRequest, CancellationToken.None);
        var endDecision = await endSut.EvaluateAsync(endRequest, CancellationToken.None);

        // Assert
        Expect(insideDecision.Allowed).To.Be.True();
        Expect(endDecision.MatchedRuleId).To.Be.Null();
    }

    [Test]
    public async Task EvaluateAsync_ShouldMatchOvernightWindowsAgainstTheirStartDay()
    {
        // Arrange
        var startDay = DayOfWeek.Monday;
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(2, 0);
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithUtcTimeWindow([startDay], start, end)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var request = PolicyRequestTestBuilder.Create().Build();
        var overnightTime = new DateTimeOffset(2026, 6, 30, 1, 0, 0, TimeSpan.Zero);
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .WithTime(overnightTime)
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(decision.MatchedConditionCategories.AsEnumerable()).To.Contain(PolicyConditionCategories.TimeWindow);
    }

    [Test]
    public async Task EvaluateAsync_ShouldNotMatchAWindowOutsideItsConfiguredUtcDay()
    {
        // Arrange
        var configuredDay = DayOfWeek.Monday;
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithUtcTimeWindow([configuredDay], new TimeOnly(9, 0), new TimeOnly(17, 0))
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var request = PolicyRequestTestBuilder.Create().Build();
        var otherDayTime = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .WithTime(otherDayTime)
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.MatchedRuleId).To.Be.Null();
    }

    [Test]
    public async Task EvaluateAsync_ShouldLoadOnlyRulesForTheRequestedGate()
    {
        // Arrange
        var sessionGate = PolicyGate.SessionStart;
        var otherGateRule = PolicyRuleTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithEffect(PolicyEffect.Deny)
            .Build();
        var repository = new FakePolicyRuleRepository([otherGateRule], returnAllRulesForGate: true);
        var request = PolicyRequestTestBuilder.Create()
            .WithGate(sessionGate)
            .Build();
        var sut = SutBuilder.Create()
            .WithRepository(repository)
            .Build();

        // Act
        var decision = await sut.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Expect(decision.Allowed).To.Be.True();
        Expect(repository.RequestedGates).To.Contain.Exactly(1).Equal.To(sessionGate);
    }

    [Test]
    public async Task EvaluateAsync_ShouldLogOnlyRedactedStructuredDecisionFields()
    {
        // Arrange
        var claimMarker = "claim-secret-marker";
        var attributeMarker = "attribute-secret-marker";
        var credentialReferenceMarker = "credential-reference-marker";
        var payloadMarker = "payload-secret-marker";
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithClaim("department", ClaimMatchOperator.AnyOf, claimMarker)
            .WithAttribute("tenant", AttributeMatchOperator.Equals, attributeMarker)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .Build();
        var identity = IdentityTestBuilder.Create()
            .WithClaim("department", claimMarker)
            .Build();
        var request = PolicyRequestTestBuilder.Create()
            .WithIdentity(identity)
            .WithAttribute("tenant", attributeMarker)
            .Build();
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithRules([rule])
            .WithLogger(logger)
            .Build();

        // Act
        await sut.EvaluateAsync(request, CancellationToken.None);
        var loggedText = logger.Entries.Single();

        // Assert
        Expect(loggedText).To.Contain("Gate");
        Expect(loggedText).To.Contain("RuleId");
        Expect(loggedText).Not.To.Contain(claimMarker);
        Expect(loggedText).Not.To.Contain(attributeMarker);
        Expect(loggedText).Not.To.Contain(credentialReferenceMarker);
        Expect(loggedText).Not.To.Contain(payloadMarker);
    }
}

file sealed class SutBuilder
{
    private IPolicyRuleRepository _repository = new FakePolicyRuleRepository([]);
    private PolicyOptions _options = PolicyOptionsTestBuilder.Create().Build();
    private TimeProvider _timeProvider = new FakeTimeProvider(
        new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero));
    private ILogger _logger = new FakeLogger();

    private SutBuilder() { }

    public static SutBuilder Create() => new();

    public SutBuilder WithRules(IEnumerable<PolicyRule> rules)
    {
        _repository = new FakePolicyRuleRepository(rules);
        return this;
    }

    public SutBuilder WithRepository(IPolicyRuleRepository repository)
    {
        _repository = repository;
        return this;
    }

    public SutBuilder WithOptions(PolicyOptions options)
    {
        _options = options;
        return this;
    }

    public SutBuilder WithTime(DateTimeOffset now)
    {
        _timeProvider = new FakeTimeProvider(now);
        return this;
    }

    public SutBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    public DeterministicPolicyEngine Build() => new(_repository, _options, _timeProvider, _logger);
}

file sealed class FakePolicyRuleRepository(IEnumerable<PolicyRule> rules) : IPolicyRuleRepository
{
    private readonly List<PolicyRule> _rules = [.. rules];
    private readonly bool _returnAllRulesForGate;

    public FakePolicyRuleRepository(
        IEnumerable<PolicyRule> rules,
        bool returnAllRulesForGate = false)
        : this(rules)
    {
        _returnAllRulesForGate = returnAllRulesForGate;
    }

    public List<PolicyGate> RequestedGates { get; } = [];

    public Task<PolicyRule?> GetAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_rules.SingleOrDefault(rule => StringComparer.Ordinal.Equals(rule.Id, id)));

    public Task<IReadOnlyList<PolicyRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PolicyRule>>(_rules);

    public Task<IReadOnlyList<PolicyRule>> GetByGateAsync(
        PolicyGate gate,
        CancellationToken cancellationToken = default)
    {
        RequestedGates.Add(gate);
        return Task.FromResult<IReadOnlyList<PolicyRule>>(
            _returnAllRulesForGate
                ? _rules
                : _rules.Where(rule => rule.Gate == gate).ToArray());
    }

    public Task SaveAsync(PolicyRule rule, CancellationToken cancellationToken = default)
    {
        var existing = _rules.FindIndex(candidate => StringComparer.Ordinal.Equals(candidate.Id, rule.Id));
        if (existing < 0)
        {
            _rules.Add(rule);
        }
        else
        {
            _rules[existing] = rule;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _rules.RemoveAll(rule => StringComparer.Ordinal.Equals(rule.Id, id));
        return Task.CompletedTask;
    }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

file sealed class FakeLogger : ILogger
{
    public List<string> Entries { get; } = [];

    public void LogTrace(string message, params object?[] args) => Capture(message, args);
    public void LogDebug(string message, params object?[] args) => Capture(message, args);
    public void LogInformation(string message, params object?[] args) => Capture(message, args);
    public void LogWarning(string message, params object?[] args) => Capture(message, args);
    public void LogError(Exception? exception, string message, params object?[] args) => Capture(message, args);
    public void LogCritical(Exception? exception, string message, params object?[] args) => Capture(message, args);

    private void Capture(string message, IEnumerable<object?> args)
    {
        Entries.Add($"{message}|{string.Join("|", args.Select(argument => argument?.ToString()))}");
    }
}
