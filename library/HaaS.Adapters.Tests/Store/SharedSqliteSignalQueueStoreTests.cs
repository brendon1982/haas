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
        var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var sut = new SharedSqliteSignalQueueStore(_dbPath, timeProvider);
        await sut.EnqueueAsync(SignalTestBuilder.Create().Build(), Identity.Anonymous);
        var dequeued = await sut.DequeueAsync();

        // Act
        await sut.NackAsync(dequeued!.Id);

        // Assert - should not be visible immediately
        var immediate = await sut.DequeueAsync();
        Expect(immediate).To.Be.Null();

        // Advance time - 2^1 = 2 seconds
        timeProvider.Advance(TimeSpan.FromSeconds(2.1));
        var visible = await sut.DequeueAsync();
        Expect(visible).Not.To.Be.Null();
        Expect(visible!.Id).To.Equal(dequeued.Id);
        Expect(visible.RetryCount).To.Equal(1);
    }

    [Test]
    public async Task Nack_MaxRetriesReached_ShouldMarkAsFailed()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var sut = new SharedSqliteSignalQueueStore(_dbPath, timeProvider);
        await sut.EnqueueAsync(SignalTestBuilder.Create().Build(), Identity.Anonymous);
        
        // 1st attempt
        var d1 = await sut.DequeueAsync();
        await sut.NackAsync(d1!.Id);
        
        // 2nd attempt
        timeProvider.Advance(TimeSpan.FromSeconds(2.1));
        var d2 = await sut.DequeueAsync();
        await sut.NackAsync(d2!.Id);
        
        // 3rd attempt
        timeProvider.Advance(TimeSpan.FromSeconds(4.1));
        var d3 = await sut.DequeueAsync();

        // Act
        await sut.NackAsync(d3!.Id, "permanent failure");

        // Assert
        var result = await sut.DequeueAsync();
        Expect(result).To.Be.Null(); // Should not be pending anymore

        // Check DB state directly to verify 'failed' status
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT status, last_error FROM signal_queue WHERE id = $id;";
        command.Parameters.AddWithValue("$id", d1.Id);
        using var reader = await command.ExecuteReaderAsync();
        Expect(await reader.ReadAsync()).To.Be.True();
        Expect(reader.GetString(0)).To.Equal("failed");
        Expect(reader.GetString(1)).To.Equal("permanent failure");
    }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
