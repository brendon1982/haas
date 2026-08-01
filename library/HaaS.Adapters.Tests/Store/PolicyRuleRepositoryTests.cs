using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Adapters.Tests.Store;

[TestFixture]
public class PolicyRuleRepositoryTests
{
    private string _testDirectory = default!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".test-artifacts",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_testDirectory, true);
    }

    [TestCase("memory")]
    [TestCase("sqlite")]
    public async Task Repositories_ShouldProvideEquivalentCrudBehavior(string repositoryKind)
    {
        // Arrange
        var initialRule = PolicyRuleTestBuilder.Create()
            .WithId("initial-rule")
            .WithGate(PolicyGate.SessionStart)
            .WithPriority(4)
            .WithEffect(PolicyEffect.Allow)
            .Build();
        var updatedRule = PolicyRuleTestBuilder.Create()
            .WithId(initialRule.Id)
            .WithGate(PolicyGate.SessionStart)
            .WithPriority(8)
            .WithEffect(PolicyEffect.Deny)
            .Build();
        var toolRule = PolicyRuleTestBuilder.Create()
            .WithId("tool-rule")
            .WithGate(PolicyGate.ToolResolution)
            .Build();
        var sut = CreateRepository(repositoryKind);

        // Act
        await sut.SaveAsync(initialRule);
        await sut.SaveAsync(toolRule);
        await sut.SaveAsync(updatedRule);
        var loaded = await sut.GetAsync(initialRule.Id);
        var allRules = await sut.GetAllAsync();
        var sessionRules = await sut.GetByGateAsync(PolicyGate.SessionStart);
        await sut.DeleteAsync(initialRule.Id);
        var deleted = await sut.GetAsync(initialRule.Id);

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Priority).To.Equal(updatedRule.Priority);
        Expect(loaded.Effect).To.Equal(updatedRule.Effect);
        Expect(allRules.Select(rule => rule.Id).ToArray()).To.Deep.Equal(
            new[] { initialRule.Id, toolRule.Id });
        Expect(sessionRules.Select(rule => rule.Id)).To.Contain.Exactly(1)
            .Equal.To(initialRule.Id);
        Expect(deleted).To.Be.Null();
    }

    [TestCase("memory")]
    [TestCase("sqlite")]
    public async Task Repositories_ShouldRoundTripEveryTypedCondition(string repositoryKind)
    {
        // Arrange
        var source = "webhook";
        var issuer = "https://issuer.example";
        var subject = "subject-42";
        var role = "approver";
        var expectedClaimOperator = ClaimMatchOperator.AllOf;
        var expectedAttributeOperator = AttributeMatchOperator.AnyOf;
        var tool = "deploy";
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Friday };
        var start = new TimeOnly(22, 15);
        var end = new TimeOnly(6, 45);
        var conditions = PolicyConditionsTestBuilder.Create()
            .WithSource(source)
            .WithSubject(issuer, subject)
            .WithRole(role)
            .WithClaim("has-access", ClaimMatchOperator.Exists)
            .WithClaim("no-suspension", ClaimMatchOperator.Absent)
            .WithClaim("department", ClaimMatchOperator.AnyOf, "engineering", "operations")
            .WithClaim("entitlement", expectedClaimOperator, "deploy", "audit")
            .WithAttribute("region", AttributeMatchOperator.Exists)
            .WithAttribute("blocked", AttributeMatchOperator.Absent)
            .WithAttribute("tenant", AttributeMatchOperator.Equals, "contoso")
            .WithAttribute("environment", AttributeMatchOperator.NotEquals, "production")
            .WithAttribute("classification", expectedAttributeOperator, "internal", "restricted")
            .WithTool(tool)
            .WithUtcTimeWindow(days, start, end)
            .Build();
        var rule = PolicyRuleTestBuilder.Create()
            .WithGate(PolicyGate.ToolResolution)
            .WithConditions(conditions)
            .Build();
        var sut = CreateRepository(repositoryKind);

        // Act
        await sut.SaveAsync(rule);
        var loaded = await sut.GetAsync(rule.Id);

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Gate).To.Equal(rule.Gate);
        Expect(loaded.Conditions.SourceTypes.AsEnumerable()).To.Deep.Equal(
            rule.Conditions.SourceTypes.AsEnumerable());
        Expect(loaded.Conditions.Subjects
            .Select(value => $"{value.Issuer}\0{value.Subject}")
            .ToArray()).To.Deep.Equal(rule.Conditions.Subjects
            .Select(value => $"{value.Issuer}\0{value.Subject}")
            .ToArray());
        Expect(loaded.Conditions.Roles.AsEnumerable()).To.Deep.Equal(
            rule.Conditions.Roles.AsEnumerable());
        Expect(loaded.Conditions.Claims
            .Select(value =>
                $"{value.ClaimType}\0{value.Operator}\0{string.Join("\0", value.Values)}")
            .ToArray()).To.Deep.Equal(rule.Conditions.Claims
            .Select(value =>
                $"{value.ClaimType}\0{value.Operator}\0{string.Join("\0", value.Values)}")
            .ToArray());
        Expect(loaded.Conditions.Attributes
            .Select(value =>
                $"{value.AttributeName}\0{value.Operator}\0{string.Join("\0", value.Values)}")
            .ToArray()).To.Deep.Equal(rule.Conditions.Attributes
            .Select(value =>
                $"{value.AttributeName}\0{value.Operator}\0{string.Join("\0", value.Values)}")
            .ToArray());
        Expect(loaded.Conditions.ToolNames.AsEnumerable()).To.Deep.Equal(
            rule.Conditions.ToolNames.AsEnumerable());
        Expect(loaded.Conditions.TimeWindows
            .Select(value =>
                $"{string.Join(",", value.Days.Order())}\0{value.Start:O}\0{value.End:O}")
            .ToArray()).To.Deep.Equal(rule.Conditions.TimeWindows
            .Select(value =>
                $"{string.Join(",", value.Days.Order())}\0{value.Start:O}\0{value.End:O}")
            .ToArray());
        Expect(loaded.Conditions.Claims.Any(claim => claim.Operator == expectedClaimOperator))
            .To.Be.True();
        Expect(loaded.Conditions.Attributes.Any(attribute => attribute.Operator == expectedAttributeOperator))
            .To.Be.True();
    }

    [Test]
    public async Task SqliteRepository_ShouldRejectMalformedAndUnknownTypedConditionData()
    {
        // Arrange
        var databasePath = Path.Combine(_testDirectory, "policies.db");
        var ruleId = "invalid-rule";
        var gate = PolicyGate.ToolResolution.ToString();
        var effect = PolicyEffect.Allow.ToString();
        var malformedConditions = "{";
        var unknownOperatorConditions =
            """
            {"sourceTypes":[],"subjects":[],"roles":[],"claims":[{"claimType":"department","operator":"Unknown","values":[]}],"attributes":[],"toolNames":[],"timeWindows":[]}
            """;
        var validEmptyConditions =
            """
            {"sourceTypes":[],"subjects":[],"roles":[],"claims":[],"attributes":[],"toolNames":[],"timeWindows":[]}
            """;
        var unknownGate = "UnknownGate";
        var sut = new SharedSqlitePolicyRuleRepository(databasePath);
        await InsertRuleAsync(databasePath, ruleId, gate, effect, malformedConditions);
        await InsertRuleAsync(databasePath, "unknown-operator-rule", gate, effect, unknownOperatorConditions);
        await InsertRuleAsync(databasePath, "unknown-gate-rule", unknownGate, effect, validEmptyConditions);

        // Act & Assert
        Expect(async () => await sut.GetAsync(ruleId))
            .To.Throw<InvalidOperationException>();
        Expect(async () => await sut.GetAsync("unknown-operator-rule"))
            .To.Throw<InvalidOperationException>();
        Expect(async () => await sut.GetAsync("unknown-gate-rule"))
            .To.Throw<InvalidOperationException>();
    }

    [Test]
    public void SqliteRepository_ShouldCreateTheDedicatedExplicitSchema()
    {
        // Arrange
        var databasePath = Path.Combine(_testDirectory, "policies.db");
        var expectedColumns = new[]
        {
            "id",
            "gate",
            "priority",
            "effect",
            "conditions_json",
            "created_at",
            "updated_at"
        };

        // Act
        _ = new SharedSqlitePolicyRuleRepository(databasePath);
        var actualColumns = ReadColumns(databasePath);

        // Assert
        Expect(actualColumns).To.Deep.Equal(expectedColumns);
    }

    private IPolicyRuleRepository CreateRepository(string repositoryKind)
    {
        var sqliteRepository = "sqlite";
        return repositoryKind == sqliteRepository
            ? new SharedSqlitePolicyRuleRepository(Path.Combine(_testDirectory, "policies.db"))
            : new InMemoryPolicyRuleRepository();
    }

    private static async Task InsertRuleAsync(
        string databasePath,
        string id,
        string gate,
        string effect,
        string conditionsJson)
    {
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO policy_rules (
                id, gate, priority, effect, conditions_json, created_at, updated_at
            ) VALUES (
                $id, $gate, $priority, $effect, $conditions, $created, $updated
            )
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$gate", gate);
        command.Parameters.AddWithValue("$priority", 1);
        command.Parameters.AddWithValue("$effect", effect);
        command.Parameters.AddWithValue("$conditions", conditionsJson);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UnixEpoch.ToString("O"));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UnixEpoch.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static string[] ReadColumns(string databasePath)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(policy_rules);";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return [.. columns];
    }
}
