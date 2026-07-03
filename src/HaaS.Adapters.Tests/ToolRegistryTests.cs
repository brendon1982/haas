using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ToolRegistryTests
{
    [Test]
    public void Register_AndGetTools_ReturnsAIFunctionWithMatchingName()
    {
        // Arrange
        var sut = new ToolRegistry();
        sut.Register("greet", (Func<string, Task<string>>)(async name => $"Hello {name}"), "Greets a person");

        // Act
        var tools = sut.GetTools(["greet"]);

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("greet");
        Expect(tools[0].Description).To.Equal("Greets a person");
    }

    [Test]
    public void GetTools_WithEmptyNames_ReturnsEmpty()
    {
        // Arrange
        var sut = new ToolRegistry();
        sut.Register("greet", (Func<string, Task<string>>)(async name => $"Hello {name}"));

        // Act
        var tools = sut.GetTools([]);

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithUnknownName_ReturnsEmpty()
    {
        // Arrange
        var sut = new ToolRegistry();

        // Act
        var tools = sut.GetTools(["nonexistent"]);

        // Assert
        Expect(tools.Count).To.Equal(0);
    }

    [Test]
    public void GetTools_WithMixOfKnownAndUnknown_ReturnsOnlyKnown()
    {
        // Arrange
        var sut = new ToolRegistry();
        sut.Register("a", (Func<Task>)(async () => { }));

        // Act
        var tools = sut.GetTools(["a", "b"]);

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("a");
    }

    [Test]
    public void Register_WithoutDescription_StillCreatesTool()
    {
        // Arrange
        var sut = new ToolRegistry();
        sut.Register("greet", (Func<string, Task<string>>)(async name => $"Hello {name}"));

        // Act
        var tools = sut.GetTools(["greet"]);

        // Assert
        Expect(tools.Count).To.Equal(1);
        Expect(tools[0].Name).To.Equal("greet");
        Expect(tools[0].Description).To.Equal("");
    }
}
