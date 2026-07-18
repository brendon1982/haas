using HaaS.Adapters.Agent;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NExpect;
using static NExpect.Expectations;
using HaaS.Domain.Ports;
using HaaS.Host.CLI;

namespace HaaS.Host.CLI.Tests;

[TestFixture]
public class ProviderRegistrationTests
{
    [Test]
    public void ChatClientFactory_WithoutInMemoryConfig_ShouldNotHaveOllama()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHaas()
            .WithTerminalGui()
            .AddSignalSource<ChatSignalSource, GuiSignalPresenter>(config =>
            {
                config.UseProvider("ollama")
                    .UseModel("gemma4");
            });
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetRequiredService<IChatClientFactory>();
        var canCreate = factory.CanCreate("ollama");

        // Assert
        Expect(canCreate).To.Be.False();
    }

    [Test]
    public void ChatClientFactory_WithInMemoryConfig_ShouldHaveOllama()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHaas()
            .WithInMemoryConfig(c => c.UseOllama())
            .WithTerminalGui()
            .AddSignalSource<ChatSignalSource, GuiSignalPresenter>(config =>
            {
                config.UseProvider("ollama")
                    .UseModel("gemma4");
            });
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetRequiredService<IChatClientFactory>();
        var canCreate = factory.CanCreate("ollama");

        // Assert
        Expect(canCreate).To.Be.True();
    }

    [Test]
    public async Task CreateAsync_WithInMemoryConfigAndSqlite_ShouldSucceedIfConfigExcluded()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHaas()
            .WithSqlitePersistence("data", includeConfig: false)
            .WithInMemoryConfig(c => c.UseOllama());
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IChatClientFactory>();

        // Act
        var client = await factory.CreateAsync("ollama", "gemma4");

        // Assert
        Expect(client).Not.To.Be.Null();
    }

    [Test]
    public async Task CreateAsync_WithInMemoryConfigAndSqlite_ShouldFailIfConfigIncluded()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHaas()
            .WithInMemoryConfig(c => c.UseOllama())
            .WithSqlitePersistence("data"); // includeConfig: true by default
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IChatClientFactory>();

        // Act & Assert
        Expect(async () => await factory.CreateAsync("ollama", "gemma4"))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("No provider configuration found for 'ollama'");
    }
}
