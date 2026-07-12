using System.Text.Json;
using Microsoft.Data.Sqlite;
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
    private string _connectionString = default!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath
        }.ToString();
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
    public async Task GetBySourceTypeAsync_ShouldReturnPersistedConfig()
    {
        // Arrange
        var sut = new SharedSqliteSignalSourceConfigRepository(_dbPath);
        var config = new SignalSourceConfig(
            "cli", "openai", "gpt-4", "System prompt", 
            new ToolBelt(["tool1"]), "off");
        await SeedConfigAsync(config);

        // Act
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

    private async Task SeedConfigAsync(SignalSourceConfig config)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO signal_source_configs (
                SourceType, Provider, ModelId, SystemPrompt, ToolBelt, ThinkingLevel
            ) VALUES (
                $type, $provider, $model, $prompt, $tools, $thinking
            );";

        command.Parameters.AddWithValue("$type", config.SourceType);
        command.Parameters.AddWithValue("$provider", config.Provider);
        command.Parameters.AddWithValue("$model", config.ModelId);
        command.Parameters.AddWithValue("$prompt", config.SystemPrompt);
        command.Parameters.AddWithValue("$tools", JsonSerializer.Serialize(config.ToolBelt));
        command.Parameters.AddWithValue("$thinking", config.ThinkingLevel);

        await command.ExecuteNonQueryAsync();
    }
}
