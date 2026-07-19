using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ToolProviderTests
{
    [Test]
    public void Register_AndGetTools_ReturnsDefinitionWithMatchingName()
    {
        // Arrange
        var sut = SutBuilder.Create().Build();
        var def = ToolDefinitionTestBuilder.Create()
            .WithName("greet")
            .WithDescription("Greets a person")
            .WithHandler((Func<string, Task<string>>)(async name => $"Hello {name}"))
            .Build();
        sut.Register(def);

        // Act
        var tools = sut.GetTools(["greet"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("greet");
        Expect(tools[0].Description).To.Equal("Greets a person");
    }

    [Test]
    public void GetTools_WithEmptyNames_ReturnsEmpty()
    {
        // Arrange
        var sut = SutBuilder.Create().Build();
        sut.Register(ToolDefinitionTestBuilder.Create().WithName("greet").Build());

        // Act
        var tools = sut.GetTools([]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithUnknownName_ReturnsEmpty()
    {
        // Arrange
        var sut = SutBuilder.Create().Build();

        // Act
        var tools = sut.GetTools(["nonexistent"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithMixOfKnownAndUnknown_ReturnsOnlyKnown()
    {
        // Arrange
        var sut = SutBuilder.Create().Build();
        sut.Register(ToolDefinitionTestBuilder.Create().WithName("a").Build());

        // Act
        var tools = sut.GetTools(["a", "b"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("a");
    }

    [Test]
    public void Register_WithDuplicateName_LastOneWins()
    {
        // Arrange
        var sut = SutBuilder.Create().Build();
        var first = ToolDefinitionTestBuilder.Create().WithName("tool").WithDescription("first").Build();
        var second = ToolDefinitionTestBuilder.Create().WithName("tool").WithDescription("second").Build();

        // Act
        sut.Register(first);
        sut.Register(second);

        // Assert
        var tools = sut.GetTools(["tool"]).ToList();
        Expect(tools).To.Contain.Exactly(1);
        Expect(tools[0].Description).To.Equal("second");
    }

    [Test]
    public async Task RegisterGeneric_ShouldResolveFromScopeAndExecute()
    {
        // Arrange
        var myTool = new MyTestTool();
        var services = new ServiceCollection();
        services.AddSingleton(myTool);
        var sp = services.BuildServiceProvider();

        var scopeAccessor = new FakeScopeAccessor { ServiceProvider = sp };

        var sut = SutBuilder.Create()
            .WithScopeAccessor(scopeAccessor)
            .Build();

        sut.Register<MyTestTool>("greet", "Greets", t => (Func<string, Task<string>>)t.GreetAsync);

        // Act
        var tools = sut.GetTools(["greet"]).ToList();
        var result = await (Task<string>)tools[0].Handler.DynamicInvoke("Junie")!;

        // Assert
        Expect(result).To.Equal("Hello Junie");
        Expect(myTool.Called).To.Be.True();
    }

    public class MyTestTool
    {
        public bool Called { get; private set; }
        public async Task<string> GreetAsync(string name)
        {
            Called = true;
            return await Task.FromResult($"Hello {name}");
        }
    }
}

// --- harness ---

file sealed class SutBuilder
{
    private ISignalScopeAccessor _scopeAccessor = new FakeScopeAccessor();

    public static SutBuilder Create() => new();

    public SutBuilder WithScopeAccessor(ISignalScopeAccessor scopeAccessor)
    {
        _scopeAccessor = scopeAccessor;
        return this;
    }

    public ToolProvider Build()
    {
        return new ToolProvider(_scopeAccessor);
    }
}

file sealed class FakeScopeAccessor : ISignalScopeAccessor
{
    public IServiceProvider ServiceProvider { get; set; } = new ServiceCollection().BuildServiceProvider();
}
