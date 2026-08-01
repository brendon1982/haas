using HaaS.Adapters.Store;
using HaaS.Application.Policies;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Infrastructure.Tests;

[TestFixture]
public class GovernanceConfigurationTests
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

    [Test]
    public async Task AddHaas_ShouldRegisterDefaultGovernanceServicesAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHaas();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<PolicyOptions>();
        var repository = provider.GetRequiredService<IPolicyRuleRepository>();
        var engine = provider.GetRequiredService<IPolicyEngine>();
        var list = provider.GetRequiredService<IListPolicyRulesUseCase>();
        var rules = await list.ExecuteAsync();

        // Assert
        Expect(options.SessionStartFallback).To.Equal(PolicyEffect.Allow);
        Expect(options.ToolResolutionFallback).To.Equal(PolicyEffect.Allow);
        Expect(options.RoleClaimType).To.Equal("role");
        Expect(repository).To.Be.An.Instance.Of<InMemoryPolicyRuleRepository>();
        Expect(engine).To.Be.An.Instance.Of<DeterministicPolicyEngine>();
        Expect(rules.Count).To.Equal(0);
    }

    [Test]
    public async Task WithGovernance_ShouldConfigureEveryRuleConditionAndSeedTheSharedRepository()
    {
        // Arrange
        var ruleId = "full-seed";
        var source = "webhook";
        var issuer = "https://issuer.example";
        var subject = "subject-1";
        var role = "approver";
        var tool = "deploy";
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);
        var seedTime = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var services = new ServiceCollection();
        var builder = services.AddHaas();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(seedTime));

        // Act
        builder.WithGovernance(governance => governance
                .WithSessionStartFallback(PolicyEffect.Deny)
                .WithToolResolutionFallback(PolicyEffect.Deny)
                .WithRoleClaimType("groups")
                .AddRule(ruleId, rule => rule
                    .WithGate(PolicyGate.ToolResolution)
                    .WithPriority(7)
                    .Allow()
                    .WithSource(source)
                    .WithSubject(issuer, subject)
                    .WithRole(role)
                    .WithClaim("department", ClaimMatchOperator.AnyOf, "engineering")
                    .WithAttribute("tenant", AttributeMatchOperator.Equals, "contoso")
                    .WithTool(tool)
                    .WithUtcTimeWindow([DayOfWeek.Monday], start, end)));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<PolicyOptions>();
        var repository = provider.GetRequiredService<IPolicyRuleRepository>();
        var save = provider.GetRequiredService<ISavePolicyRuleUseCase>();
        var loaded = await repository.GetAsync(ruleId);

        // Assert
        Expect(options.SessionStartFallback).To.Equal(PolicyEffect.Deny);
        Expect(options.ToolResolutionFallback).To.Equal(PolicyEffect.Deny);
        Expect(options.RoleClaimType).To.Equal("groups");
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.CreatedAt).To.Equal(seedTime);
        Expect(loaded!.Conditions.SourceTypes.Length).To.Equal(1);
        Expect(loaded.Conditions.SourceTypes[0]).To.Equal(source);
        Expect(loaded.Conditions.Subjects[0].Issuer).To.Equal(issuer);
        Expect(loaded.Conditions.Subjects[0].Subject).To.Equal(subject);
        Expect(loaded.Conditions.Roles.Length).To.Equal(1);
        Expect(loaded.Conditions.Roles[0]).To.Equal(role);
        Expect(loaded.Conditions.Claims.Length).To.Equal(1);
        Expect(loaded.Conditions.Attributes.Length).To.Equal(1);
        Expect(loaded.Conditions.ToolNames.Length).To.Equal(1);
        Expect(loaded.Conditions.ToolNames[0]).To.Equal(tool);
        Expect(loaded.Conditions.TimeWindows.Length).To.Equal(1);
        Expect(save).Not.To.Be.Null();
    }

    [Test]
    public async Task WithSqlitePersistence_ShouldUsePoliciesDbWithoutConfigAndPreserveRuntimeEditsAcrossSeeds()
    {
        // Arrange
        var databaseDirectory = Path.Combine(_testDirectory, "database");
        var ruleId = "seed-rule";
        var seedPriority = 1;
        var runtimePriority = 10;
        var firstServices = new ServiceCollection();
        firstServices.AddHaas()
            .WithSqlitePersistence(databaseDirectory, includeConfig: false)
            .WithGovernance(governance => governance.AddRule(ruleId, rule => rule
                .WithGate(PolicyGate.SessionStart)
                .WithPriority(seedPriority)
                .Allow()));

        // Act
        Type firstRepositoryType;
        using (var firstProvider = firstServices.BuildServiceProvider())
        {
            var repository = firstProvider.GetRequiredService<IPolicyRuleRepository>();
            firstRepositoryType = repository.GetType();
            var seeded = await repository.GetAsync(ruleId);
            var runtimeRule = PolicyRuleTestBuilder.Create()
                .WithId(ruleId)
                .WithGate(PolicyGate.SessionStart)
                .WithPriority(runtimePriority)
                .WithEffect(PolicyEffect.Deny)
                .Build();
            var save = firstProvider.GetRequiredService<ISavePolicyRuleUseCase>();
            await save.ExecuteAsync(runtimeRule);
        }

        var secondServices = new ServiceCollection();
        secondServices.AddHaas()
            .WithGovernance(governance => governance.AddRule(ruleId, rule => rule
                .WithGate(PolicyGate.SessionStart)
                .WithPriority(seedPriority)
                .Allow()))
            .WithSqlitePersistence(databaseDirectory, includeConfig: false);
        using var secondProvider = secondServices.BuildServiceProvider();
        var selectedRepository = secondProvider.GetRequiredService<IPolicyRuleRepository>();
        var configRepository = secondProvider.GetRequiredService<ISignalSourceConfigRepository>();
        var persisted = await selectedRepository.GetAsync(ruleId);

        // Assert
        Expect(firstRepositoryType).To.Equal(typeof(SharedSqlitePolicyRuleRepository));
        Expect(selectedRepository).To.Be.An.Instance.Of<SharedSqlitePolicyRuleRepository>();
        Expect(configRepository).To.Be.An.Instance.Of<InMemorySignalSourceConfigRepository>();
        Expect(File.Exists(Path.Combine(databaseDirectory, "policies.db"))).To.Be.True();
        Expect(persisted).Not.To.Be.Null();
        Expect(persisted!.Priority).To.Equal(runtimePriority);
        Expect(persisted.Effect).To.Equal(PolicyEffect.Deny);
    }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
