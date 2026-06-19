using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.ValueObjects;

[TestFixture]
public class SignalTests
{
    [Test]
    public void Create_WithPayloadAndSource_SetsProperties()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello")
            .WithSource("cli")
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(signal.Payload, Is.EqualTo("hello"));
            Assert.That(signal.Source, Is.EqualTo("cli"));
            Assert.That(signal.SessionId, Is.Null);
        });
    }

    [Test]
    public void Create_WithSessionId_SetsSessionId()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello")
            .WithSource("cli")
            .WithSessionId("sess-1")
            .Build();

        // Act & Assert
        Assert.That(signal.SessionId, Is.EqualTo("sess-1"));
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
