using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Adapters.Tests.Store;

[TestFixture]
public class SharedSqliteSignalSourceConfigRepositoryTests
{
    private string _dbPath = default!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Test]
    public async Task SaveAndLoad_ShouldPersistConfig()
    {
        // Arrange
        var sut = new SharedSqliteSignalSourceConfigRepository(_dbPath);
        var config = new SignalSourceConfig(
            "cli", "openai", "gpt-4", "System prompt", 
            new ToolBelt(["tool1"]), "off");

        // Act
        await sut.SaveAsync(config);
        var loaded = await sut.GetBySourceTypeAsync("cli");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.SourceType).To.Equal(config.SourceType);
        Expect(loaded.Provider).To.Equal(config.Provider);
        Expect(loaded.ModelId).To.Equal(config.ModelId);
        Expect(loaded.SystemPrompt).To.Equal(config.SystemPrompt);
        Expect(loaded.ToolBelt.Tools).To.Deep.Equal(config.ToolBelt.Tools);
        Expect(loaded.ThinkingLevel).To.Equal(config.ThinkingLevel);
    }

    [Test]
    public async Task Save_Twice_ShouldUpdateConfig()
    {
        // Arrange
        var sut = new SharedSqliteSignalSourceConfigRepository(_dbPath);
        var config = new SignalSourceConfig(
            "cli", "openai", "gpt-4", "System prompt", 
            ToolBelt.Empty, "off");
        await sut.SaveAsync(config);

        var updated = config with { ModelId = "gpt-4o", ThinkingLevel = "high" };

        // Act
        await sut.SaveAsync(updated);
        var loaded = await sut.GetBySourceTypeAsync("cli");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.ModelId).To.Equal("gpt-4o");
        Expect(loaded.ThinkingLevel).To.Equal("high");
    }
}
