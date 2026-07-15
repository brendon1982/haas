using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Adapters.Tests.Store;

[TestFixture]
public class PerSessionSqliteMessageStoreTests
{
    private string _baseDir = default!;

    [SetUp]
    public void SetUp()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_baseDir))
        {
            Directory.Delete(_baseDir, true);
        }
    }

    [Test]
    public async Task AppendAndGet_ShouldPersistMessagesPerSession()
    {
        // Arrange
        var sut = new PerSessionSqliteMessageStore(_baseDir);
        var sessionId1 = "sess-1";
        var sessionId2 = "sess-2";
        var messages1 = new[] 
        { 
            new DomainMessage("user", "hello from 1", DateTimeOffset.UtcNow), 
            new DomainMessage("assistant", "how are you from 1", DateTimeOffset.UtcNow) 
        };
        var messages2 = new[] 
        { 
            new DomainMessage("user", "hello from 2", DateTimeOffset.UtcNow) 
        };

        // Act
        await sut.AppendMessagesAsync(sessionId1, messages1);
        await sut.AppendMessagesAsync(sessionId2, messages2);
        
        var loaded1 = await sut.GetMessagesAsync(sessionId1);
        var loaded2 = await sut.GetMessagesAsync(sessionId2);

        // Assert
        Expect(loaded1).To.Deep.Equal(messages1);
        Expect(loaded2).To.Deep.Equal(messages2);
    }

    [Test]
    public async Task GetMessageCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var sut = new PerSessionSqliteMessageStore(_baseDir);
        var sessionId = "sess-1";
        await sut.AppendMessagesAsync(sessionId, [
            new DomainMessage("user", "msg1", DateTimeOffset.UtcNow), 
            new DomainMessage("assistant", "msg2", DateTimeOffset.UtcNow)
        ]);

        // Act
        var count = await sut.GetMessageCountAsync(sessionId);

        // Assert
        Expect(count).To.Equal(2);
    }

    [Test]
    public async Task GetMessages_WhenSessionDoesNotExist_ShouldReturnEmpty()
    {
        // Arrange
        var sut = new PerSessionSqliteMessageStore(_baseDir);

        // Act
        var messages = await sut.GetMessagesAsync("non-existent");

        // Assert
        Expect(messages.Count).To.Equal(0);
    }
}
