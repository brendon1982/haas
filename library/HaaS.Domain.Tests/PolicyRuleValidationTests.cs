using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Domain.Tests;

[TestFixture]
public class PolicyRuleValidationTests
{
    [Test]
    public void Build_ShouldRejectEmptyRuleId()
    {
        // Arrange
        var emptyId = " ";
        var builder = PolicyRuleTestBuilder.Create()
            .WithId(emptyId);

        // Act
        Action action = () => builder.Build();

        // Assert
        Expect(action).To.Throw<ArgumentException>();
    }

    [Test]
    public void Build_ShouldRejectToolConditionForSessionStart()
    {
        // Arrange
        var toolName = "deploy";
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithTool(toolName)
            .Build();
        var builder = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions);

        // Act
        Action action = () => builder.Build();

        // Assert
        Expect(action).To.Throw<ArgumentException>();
    }

    [Test]
    public void Build_ShouldRejectClaimOperatorValuesThatDoNotMatchItsShape()
    {
        // Arrange
        var claimType = "department";
        var unexpectedValue = "engineering";
        var conditionsBuilder = PolicyConditionsTestBuilder.Create();

        // Act
        Action action = () => PolicyRuleTestBuilder.Create()
            .WithConditions(conditionsBuilder
                .WithClaim(claimType, ClaimMatchOperator.Exists, unexpectedValue)
                .Build())
            .Build();

        // Assert
        Expect(action).To.Throw<ArgumentException>();
    }

    [Test]
    public void Build_ShouldRejectEmptyTimeWindows()
    {
        // Arrange
        var start = new TimeOnly(12, 0);
        var conditionsBuilder = PolicyConditionsTestBuilder.Create();

        // Act
        Action action = () => PolicyRuleTestBuilder.Create()
            .WithConditions(conditionsBuilder
                .WithUtcTimeWindow([], start, start)
                .Build())
            .Build();

        // Assert
        Expect(action).To.Throw<ArgumentException>();
    }

    [Test]
    public void Build_ShouldRejectUnsupportedGateAndEffectValues()
    {
        // Arrange
        var invalidGate = (PolicyGate)99;
        var invalidEffect = (PolicyEffect)99;

        // Act
        Action invalidGateAction = () => PolicyRuleTestBuilder.Create()
            .WithGate(invalidGate)
            .Build();
        Action invalidEffectAction = () => PolicyRuleTestBuilder.Create()
            .WithEffect(invalidEffect)
            .Build();

        // Assert
        Expect(invalidGateAction).To.Throw<ArgumentOutOfRangeException>();
        Expect(invalidEffectAction).To.Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Build_ShouldNormalizeConditionCollectionsAndTimestamps()
    {
        // Arrange
        var source = "webhook";
        var laterTimestamp = new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.FromHours(3));
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithSource(source)
            .WithSource(source)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithConditions(conditions)
            .WithCreatedAt(laterTimestamp)
            .WithUpdatedAt(laterTimestamp)
            .Build();

        // Act
        var sources = rule.Conditions.SourceTypes;

        // Assert
        Expect(sources.AsEnumerable()).To.Contain.Exactly(1).Equal.To(source);
        Expect(rule.CreatedAt.Offset).To.Equal(TimeSpan.Zero);
        Expect(rule.UpdatedAt.Offset).To.Equal(TimeSpan.Zero);
    }

    [Test]
    public void PolicyRequest_ShouldExposeOnlyPolicySafeInputs()
    {
        // Arrange
        var requestProperties = typeof(PolicyRequest).GetProperties();
        var forbiddenTypeNames = new[]
        {
            nameof(Signal),
            nameof(AuthenticationContext),
            nameof(CredentialReference)
        };

        // Act
        var propertyTypeNames = requestProperties
            .Select(property => property.PropertyType.Name)
            .ToArray();

        // Assert
        foreach (var forbiddenTypeName in forbiddenTypeNames)
        {
            Expect(propertyTypeNames).Not.To.Contain(forbiddenTypeName);
        }
    }

    [Test]
    public void PolicyDecision_ShouldExposeOnlySafeDecisionMetadata()
    {
        // Arrange
        var decisionProperties = typeof(PolicyDecision).GetProperties();
        var forbiddenPropertyNames = new[] { "Claims", "Attributes", "Values", "Payload" };

        // Act
        var propertyNames = decisionProperties
            .Select(property => property.Name)
            .ToArray();

        // Assert
        foreach (var forbiddenPropertyName in forbiddenPropertyNames)
        {
            Expect(propertyNames).Not.To.Contain(forbiddenPropertyName);
        }
    }

    [Test]
    public void PolicyOptions_ShouldDefaultToAllowAndTheRoleClaim()
    {
        // Arrange
        var options = PolicyOptionsTestBuilder.Create().Build();
        var expectedRoleClaimType = "role";

        // Act
        var sessionFallback = options.GetFallback(PolicyGate.SessionStart);
        var toolFallback = options.GetFallback(PolicyGate.ToolResolution);

        // Assert
        Expect(sessionFallback).To.Equal(PolicyEffect.Allow);
        Expect(toolFallback).To.Equal(PolicyEffect.Allow);
        Expect(options.RoleClaimType).To.Equal(expectedRoleClaimType);
    }
}
