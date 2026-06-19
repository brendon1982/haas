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
        var expectedProvider = "ollama";
        var expectedModelId = "gemma4";
        var expectedPrompt = "You are a helpful assistant.";
        var expectedTools = new[] { "tool1", "tool2" };
        var expectedThinkingLevel = "high";
        var config = AgentSessionConfigTestBuilder.Create()
            .WithProvider(expectedProvider)
            .WithModelId(expectedModelId)
            .WithSystemPrompt(expectedPrompt)
            .WithTools(expectedTools)
            .WithThinkingLevel(expectedThinkingLevel)
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo(expectedProvider));
            Assert.That(config.ModelId, Is.EqualTo(expectedModelId));
            Assert.That(config.SystemPrompt, Is.EqualTo(expectedPrompt));
            Assert.That(config.Tools, Has.Count.EqualTo(expectedTools.Length));
            Assert.That(config.ThinkingLevel, Is.EqualTo(expectedThinkingLevel));
        });
    }
}
