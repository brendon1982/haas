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
            .AddSignalSource<ChatSignalSource, CliSignalPresenter>(config =>
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
            .AddSignalSource<ChatSignalSource, CliSignalPresenter>(config =>
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
}
