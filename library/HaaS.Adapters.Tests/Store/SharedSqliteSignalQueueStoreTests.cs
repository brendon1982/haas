using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;
using Microsoft.Data.Sqlite;

namespace HaaS.Adapters.Tests.Store;

[TestFixture]
public class SharedSqliteSignalQueueStoreTests
{
    private string _dbPath = default!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Test]
    public async Task EnqueueAndDequeue_ShouldRoundtripSignal()
    {
        // Arrange
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello queue")
            .WithSource("slack")
            .Build();
        var identity = new Identity("user-1", new[] { "role:admin" });

        // Act
        await sut.EnqueueAsync(signal, identity);
        var dequeued = await sut.DequeueAsync();

        // Assert
        Expect(dequeued).Not.To.Be.Null();
        Expect(dequeued!.Signal.Payload).To.Equal(signal.Payload);
        Expect(dequeued.Signal.Source).To.Equal(signal.Source);
        Expect(dequeued.Identity.Name).To.Equal(identity.Name);
        Expect(dequeued.Identity.Claims).To.Deep.Equal(identity.Claims);
        Expect(dequeued.Status).To.Equal(SignalStatus.Processing);
    }

    [Test]
    public async Task Ack_ShouldMarkAsCompleted()
    {
        // Arrange
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        await sut.EnqueueAsync(SignalTestBuilder.Create().Build(), Identity.Anonymous);
        var dequeued = await sut.DequeueAsync();

        // Act
        await sut.AckAsync(dequeued!.Id);

        // Assert
        var result = await sut.DequeueAsync();
        Expect(result).To.Be.Null(); // Nothing left in pending
    }

    [Test]
    public async Task Nack_ShouldResetToPendingAndIncrementRetry()
    {
        // Arrange
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        await sut.EnqueueAsync(SignalTestBuilder.Create().Build(), Identity.Anonymous);
        var dequeued = await sut.DequeueAsync();

        // Act
        await sut.NackAsync(dequeued!.Id);

        // Assert
        var result = await sut.DequeueAsync();
        Expect(result).Not.To.Be.Null();
        Expect(result!.Id).To.Equal(dequeued.Id);
        Expect(result.RetryCount).To.Equal(1);
    }
}
