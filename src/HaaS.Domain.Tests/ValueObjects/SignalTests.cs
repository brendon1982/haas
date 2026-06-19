using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Domain.Tests.ValueObjects;

[TestFixture]
public class SignalTests
{
    [Test]
    public void Create_WithPayloadAndSource_SetsProperties()
    {
        // Arrange
        var expectedPayload = "hello";
        var expectedSource = "cli";
        var signal = SignalTestBuilder.Create()
            .WithPayload(expectedPayload)
            .WithSource(expectedSource)
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(signal.Payload, Is.EqualTo(expectedPayload));
            Assert.That(signal.Source, Is.EqualTo(expectedSource));
            Assert.That(signal.SessionId, Is.Null);
        });
    }

    [Test]
    public void Create_WithSessionId_SetsSessionId()
    {
        // Arrange
        var expectedSessionId = "sess-1";
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello")
            .WithSource("cli")
            .WithSessionId(expectedSessionId)
            .Build();

        // Act & Assert
        Assert.That(signal.SessionId, Is.EqualTo(expectedSessionId));
    }

    [Test]
    public void Create_DefaultValues_AreSensible()
    {
        // Arrange
        var signal = SignalTestBuilder.Create().Build();

        // Act & Assert
        Assert.That(signal.Payload, Is.Not.Empty);
        Assert.That(signal.Source, Is.EqualTo("test"));
    }
}
