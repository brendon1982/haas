using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class InMemoryProviderConfigRepositoryTests
{
    [Test]
    public async Task SaveAndGet_RoundTrip_ReturnsSameConfig()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();
        var config = new ProviderConfig("ollama", "http://localhost:11434");

        // Act
        await sut.SaveAsync(config);
        var loaded = await sut.GetAsync("ollama");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Provider).To.Equal(config.Provider);
        Expect(loaded.Endpoint).To.Equal(config.Endpoint);
    }

    [Test]
    public async Task Get_MissingProvider_ReturnsNull()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();

        // Act
        var loaded = await sut.GetAsync("nonexistent");

        // Assert
        Expect(loaded).To.Be.Null();
    }

    [Test]
    public async Task Save_OverwriteExisting_UpdatesConfig()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();
        await sut.SaveAsync(new ProviderConfig("ollama", "http://old:11434"));

        // Act
        await sut.SaveAsync(new ProviderConfig("ollama", "http://new:11434"));
        var loaded = await sut.GetAsync("ollama");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Endpoint).To.Equal("http://new:11434");
    }

    [Test]
    public async Task GetAll_ReturnsAllSavedConfigs()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();
        var ollama = new ProviderConfig("ollama", "http://ollama:11434");
        var openai = new ProviderConfig("openai", "http://openai:8080");
        await sut.SaveAsync(ollama);
        await sut.SaveAsync(openai);

        // Act
        var all = await sut.GetAllAsync();

        // Assert
        Expect(all.Count).To.Equal(2);
        Expect(all.Select(c => c.Provider)).To.Contain.All.Of("ollama", "openai");
    }

    [Test]
    public async Task GetAll_EmptyStore_ReturnsEmpty()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();

        // Act
        var all = await sut.GetAllAsync();

        // Assert
        Expect(all.Count).To.Equal(0);
    }
}

// --- harness (local) ---

file sealed class RepositorySutBuilder
{
    private RepositorySutBuilder() { }

    public static RepositorySutBuilder Create() => new();

    public InMemoryProviderConfigRepository Build() => new();
}
