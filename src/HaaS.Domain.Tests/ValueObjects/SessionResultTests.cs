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
        var expectedOutput = "Hello world";
        var expectedSessionId = "sess-1";
        var result = SessionResultTestBuilder.Create()
            .WithOutput(expectedOutput)
            .WithSessionId(expectedSessionId)
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Is.EqualTo(expectedOutput));
            Assert.That(result.SessionId, Is.EqualTo(expectedSessionId));
        });
    }
}
