using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Domain.Tests.ValueObjects;

[TestFixture]
public class AgentSessionConfigTests
{
    [Test]
    public void Create_WithBuilder_SetsProperties()
    {
        // Arrange
        var config = AgentSessionConfigTestBuilder.Create()
            .WithProvider("ollama")
            .WithModelId("gemma4")
            .WithSystemPrompt("You are a helpful assistant.")
            .WithTools(["tool1", "tool2"])
            .WithThinkingLevel("high")
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo("ollama"));
            Assert.That(config.ModelId, Is.EqualTo("gemma4"));
            Assert.That(config.SystemPrompt, Is.EqualTo("You are a helpful assistant."));
            Assert.That(config.Tools, Has.Count.EqualTo(2));
            Assert.That(config.ThinkingLevel, Is.EqualTo("high"));
        });
    }
}
