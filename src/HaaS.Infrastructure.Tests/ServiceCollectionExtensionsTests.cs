using HaaS.Adapters.Deferred;
using HaaS.Infrastructure;
using HaaS.Application;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NExpect;
using static NExpect.Expectations;

namespace HaaS.Infrastructure.Tests;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddHaas_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new SutBuilder()
            .Build();

        // Act
        services.AddHaas();
        var provider = services.BuildServiceProvider();

        // Assert
        Expect(() => provider.GetRequiredService<IHaasEngine>())
            .Not.To.Throw();
        Expect(() => provider.GetRequiredService<IAgentStrategy>())
            .Not.To.Throw();
        Expect(() => provider.GetRequiredService<ISessionRepository>())
            .Not.To.Throw();
        Expect(() => provider.GetRequiredService<IMessageStore>())
            .Not.To.Throw();
    }

    [Test]
    public void AddSignalSource_ShouldRegisterSourceAndPresenterWithFullConfig()
    {
        // Arrange
        var services = new SutBuilder()
            .Build();
        var builder = services.AddHaas();
        var expectedProvider = "anthropic";
        var expectedModel = "claude-3-5-sonnet";
        var expectedPrompt = "Be a pirate.";
        var expectedLevel = "high";
        var expectedTool = "calculate";

        // Act
        builder.AddSignalSource<TestSignalSource, TestSignalPresenter>(c => c
            .UseProvider(expectedProvider)
            .UseModel(expectedModel)
            .UseSystemPrompt(expectedPrompt)
            .UseThinkingLevel(expectedLevel)
            .AddTool(expectedTool));
        var provider = services.BuildServiceProvider();

        // Assert
        Expect(() => provider.GetRequiredService<TestSignalSource>())
            .Not.To.Throw();
        Expect(() => provider.GetRequiredService<TestSignalPresenter>())
            .Not.To.Throw();
        
        var registration = provider.GetRequiredService<SignalSourceRegistration>();
        Expect(registration.Source).To.Be.An.Instance.Of<TestSignalSource>();
        Expect(registration.Presenter).To.Be.An.Instance.Of<DeferredPresenter>();
        Expect(registration.Config.Provider).To.Equal(expectedProvider);
        Expect(registration.Config.ModelId).To.Equal(expectedModel);
        Expect(registration.Config.SystemPrompt).To.Equal(expectedPrompt);
        Expect(registration.Config.ThinkingLevel).To.Equal(expectedLevel);
        Expect(registration.Config.ToolBelt.Tools.AsEnumerable()).To.Contain.Exactly(1).Equal.To(expectedTool);
    }

    [Test]
    public void AddSignalSource_CalledTwice_ShouldRegisterBoth()
    {
        // Arrange
        var services = new SutBuilder()
            .Build();
        var builder = services.AddHaas();
        var provider1 = "p1";
        var provider2 = "p2";

        // Act
        builder.AddSignalSource<TestSignalSource, TestSignalPresenter>(c => c.UseProvider(provider1));
        builder.AddSignalSource<TestSignalSource, TestSignalPresenter>(c => c.UseProvider(provider2));
        var provider = services.BuildServiceProvider();

        // Assert
        var registrations = provider.GetServices<SignalSourceRegistration>().ToArray();
        Expect(registrations.Length).To.Equal(2);
        var providers = registrations.Select(r => r.Config.Provider).ToArray();
        Expect(providers).To.Contain(provider1);
        Expect(providers).To.Contain(provider2);
    }

    private class TestSignalSource : ISignalSource
    {
        public string Type => "test";
        public Task ListenAsync(Func<Signal, Task<string>> handler) => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;
    }

    private class TestSignalPresenter : ISignalPresenter
    {
        public Task PresentAsync(SessionResult result) => Task.CompletedTask;
    }
}

file sealed class SutBuilder
{
    public IServiceCollection Build()
    {
        return new ServiceCollection();
    }
}
