using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Ports;
using NUnit.Framework;
using NSubstitute;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ToolProviderTests
{
    [Test]
    public void Register_AndGetTools_ReturnsDefinitionWithMatchingName()
    {
        // Arrange
        var sut = Create().Build();
        var def = new ToolDefinition("greet", "Greets a person", (Func<string, Task<string>>)(async name => $"Hello {name}"));
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
        var sut = Create().Build();
        sut.Register(new ToolDefinition("greet", "", (Func<string, Task<string>>)(async name => $"Hello {name}")));

        // Act
        var tools = sut.GetTools([]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithUnknownName_ReturnsEmpty()
    {
        // Arrange
        var sut = Create().Build();

        // Act
        var tools = sut.GetTools(["nonexistent"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithMixOfKnownAndUnknown_ReturnsOnlyKnown()
    {
        // Arrange
        var sut = Create().Build();
        sut.Register(new ToolDefinition("a", "", (Func<Task>)(async () => { })));

        // Act
        var tools = sut.GetTools(["a", "b"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("a");
    }

    [Test]
    public async Task RegisterGeneric_ShouldResolveFromScopeAndExecute()
    {
        // Arrange
        var myTool = new MyTestTool();
        var services = new ServiceCollection();
        services.AddSingleton(myTool);
        var sp = services.BuildServiceProvider();

        var scopeAccessor = Substitute.For<ISignalScopeAccessor>();
        scopeAccessor.ServiceProvider.Returns(sp);

        var sut = Create()
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

    private static SutBuilder Create() => new();

    private sealed class SutBuilder
    {
        private ISignalScopeAccessor _scopeAccessor = Substitute.For<ISignalScopeAccessor>();

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
}
