using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ToolProviderTests
{
    [Test]
    public void Register_AndGetTools_ReturnsDefinitionWithMatchingName()
    {
        // Arrange
        var sut = new ToolProvider();
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
        var sut = new ToolProvider();
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
        var sut = new ToolProvider();

        // Act
        var tools = sut.GetTools(["nonexistent"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithMixOfKnownAndUnknown_ReturnsOnlyKnown()
    {
        // Arrange
        var sut = new ToolProvider();
        sut.Register(new ToolDefinition("a", "", (Func<Task>)(async () => { })));

        // Act
        var tools = sut.GetTools(["a", "b"]).ToList();

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("a");
    }
}
