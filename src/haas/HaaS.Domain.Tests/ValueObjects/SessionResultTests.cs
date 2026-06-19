using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Domain.Tests.ValueObjects;

[TestFixture]
public class SessionResultTests
{
    [Test]
    public void Create_WithOutputAndSessionId_SetsProperties()
    {
        // Arrange
        var result = SessionResultTestBuilder.Create()
            .WithOutput("Hello world")
            .WithSessionId("sess-1")
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Is.EqualTo("Hello world"));
            Assert.That(result.SessionId, Is.EqualTo("sess-1"));
        });
    }
}
