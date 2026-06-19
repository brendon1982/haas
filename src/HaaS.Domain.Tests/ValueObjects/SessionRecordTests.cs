using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Domain.Tests.ValueObjects;

[TestFixture]
public class SessionRecordTests
{
    [Test]
    public void Create_WithBuilder_SetsProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var state = new byte[] { 1, 2, 3 };
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithSourceType("cli")
            .WithStatus("running")
            .WithAgentState(state)
            .WithCreatedAt(now)
            .WithUpdatedAt(now)
            .Build();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(record.SessionId, Is.EqualTo("sess-1"));
            Assert.That(record.SourceType, Is.EqualTo("cli"));
            Assert.That(record.Status, Is.EqualTo("running"));
            Assert.That(record.AgentState, Is.EqualTo(state));
            Assert.That(record.CreatedAt, Is.EqualTo(now));
            Assert.That(record.UpdatedAt, Is.EqualTo(now));
        });
    }

    [Test]
    public void Create_WithNullAgentState_IsNull()
    {
        // Arrange
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithAgentState(null)
            .Build();

        // Act & Assert
        Assert.That(record.AgentState, Is.Null);
    }
}
