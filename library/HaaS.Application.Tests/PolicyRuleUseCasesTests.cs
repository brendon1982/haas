using HaaS.Adapters.Store;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Application.Tests;

[TestFixture]
public class PolicyRuleUseCasesTests
{
    [Test]
    public async Task SaveAsync_ShouldSetCreationAndUpdateTimestampsForNewRule()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var rule = PolicyRuleTestBuilder.Create()
            .WithCreatedAt(DateTimeOffset.UnixEpoch)
            .WithUpdatedAt(DateTimeOffset.UnixEpoch)
            .Build();
        var sut = SutBuilder.Create()
            .WithTime(now)
            .BuildSave();

        // Act
        var saved = await sut.ExecuteAsync(rule);

        // Assert
        Expect(saved.CreatedAt).To.Equal(now);
        Expect(saved.UpdatedAt).To.Equal(now);
    }

    [Test]
    public async Task SaveAsync_ShouldPreserveCreationAndRefreshUpdateTimestampForExistingRule()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var initial = PolicyRuleTestBuilder.Create()
            .WithCreatedAt(createdAt)
            .WithUpdatedAt(createdAt)
            .Build();
        var revised = PolicyRuleTestBuilder.Create()
            .WithId(initial.Id)
            .WithPriority(9)
            .WithEffect(PolicyEffect.Deny)
            .WithCreatedAt(DateTimeOffset.UnixEpoch)
            .WithUpdatedAt(DateTimeOffset.UnixEpoch)
            .Build();
        var sutBuilder = SutBuilder.Create()
            .WithTime(updatedAt);
        var repository = sutBuilder.Repository;
        await repository.SaveAsync(initial);
        var sut = sutBuilder.BuildSave();

        // Act
        var saved = await sut.ExecuteAsync(revised);

        // Assert
        Expect(saved.CreatedAt).To.Equal(createdAt);
        Expect(saved.UpdatedAt).To.Equal(updatedAt);
        Expect(saved.Priority).To.Equal(revised.Priority);
        Expect(saved.Effect).To.Equal(revised.Effect);
    }

    [Test]
    public async Task ListGetAndDeleteAsync_ShouldOperateOnTheSharedRepository()
    {
        // Arrange
        var rule = PolicyRuleTestBuilder.Create().Build();
        var sutBuilder = SutBuilder.Create();
        await sutBuilder.Repository.SaveAsync(rule);
        var list = sutBuilder.BuildList();
        var get = sutBuilder.BuildGet();
        var delete = sutBuilder.BuildDelete();

        // Act
        var rules = await list.ExecuteAsync();
        var loaded = await get.ExecuteAsync(rule.Id);
        await delete.ExecuteAsync(rule.Id);
        var deleted = await get.ExecuteAsync(rule.Id);

        // Assert
        Expect(rules.Select(candidate => candidate.Id)).To.Contain.Exactly(1)
            .Equal.To(rule.Id);
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Id).To.Equal(rule.Id);
        Expect(deleted).To.Be.Null();
    }
}

file sealed class SutBuilder
{
    private TimeProvider _timeProvider = new FakeTimeProvider(
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

    private SutBuilder() { }

    public IPolicyRuleRepository Repository { get; } = new InMemoryPolicyRuleRepository();

    public static SutBuilder Create() => new();

    public SutBuilder WithTime(DateTimeOffset now)
    {
        _timeProvider = new FakeTimeProvider(now);
        return this;
    }

    public SavePolicyRuleUseCase BuildSave() => new(Repository, _timeProvider);

    public ListPolicyRulesUseCase BuildList() => new(Repository);

    public GetPolicyRuleUseCase BuildGet() => new(Repository);

    public DeletePolicyRuleUseCase BuildDelete() => new(Repository);
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
