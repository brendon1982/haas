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
        _dbPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid()}.db");
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
    public async Task EnqueueAndDequeue_ShouldRoundtripCompleteEnvelope()
    {
        // Arrange
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello queue")
            .WithSource("slack")
            .WithSessionId("session-42")
            .WithArrivedAt(new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero))
            .WithMessageId("message-42")
            .Build();
        var identity = IdentityTestBuilder.Create()
            .WithIssuer("test")
            .WithSubject("user-1")
            .WithClaim("role", "admin")
            .Build();
        var context = SignalContextTestBuilder.Create()
            .WithAuthentication(AuthenticationContextTestBuilder.Create()
                .WithIdentity(identity)
                .WithAuthenticationMethod("oauth")
                .WithCredentialReference("calendar", "vault", "calendar-ref")
                .Build())
            .WithAttribute("tenant", "contoso")
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(signal)
            .WithContext(context)
            .Build();

        // Act
        await sut.EnqueueAsync(envelope);
        var dequeued = await sut.DequeueAsync();

        // Assert
        Expect(dequeued).Not.To.Be.Null();
        Expect(dequeued!.Envelope.Signal).To.Equal(signal);
        Expect(dequeued.Envelope.Context.Authentication.Identity).To.Equal(identity);
        Expect(dequeued.Envelope.Context.Authentication.Identity.Claims).To.Deep.Equal(identity.Claims);
        Expect(dequeued.Envelope.Context.Authentication.AuthenticationMethod).To.Equal("oauth");
        Expect(dequeued.Envelope.Context.Attributes).To.Deep.Equal(context.Attributes);
        Expect(dequeued.Envelope.Context.Authentication.CredentialReferences["calendar"])
            .To.Equal(context.Authentication.CredentialReferences["calendar"]);
        Expect(dequeued.Status).To.Equal(SignalStatus.Processing);
    }

    [Test]
    public async Task Ack_ShouldMarkAsCompleted()
    {
        // Arrange
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        await sut.EnqueueAsync(SignalEnvelopeTestBuilder.Create().Build());
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
        await sut.EnqueueAsync(SignalEnvelopeTestBuilder.Create().Build());
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
        await sut.EnqueueAsync(SignalEnvelopeTestBuilder.Create().Build());
        
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

    [Test]
    public async Task Constructor_WhenDatabaseContainsLegacyQueueRow_MigratesAndReturnsAnonymousContext()
    {
        // Arrange
        var expectedPayload = "legacy payload";
        var expectedSource = "legacy-source";
        var expectedSessionId = "legacy-session";
        var expectedCreatedAt = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        await CreateLegacyQueueRowAsync(expectedPayload, expectedSource, expectedSessionId, expectedCreatedAt);

        // Act
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        var dequeued = await sut.DequeueAsync();

        // Assert
        Expect(dequeued).Not.To.Be.Null();
        Expect(dequeued!.Envelope.Signal.Payload).To.Equal(expectedPayload);
        Expect(dequeued.Envelope.Signal.Source).To.Equal(expectedSource);
        Expect(dequeued.Envelope.Signal.SessionId).To.Equal(expectedSessionId);
        Expect(dequeued.Envelope.Signal.ArrivedAt).To.Be.Null();
        Expect(dequeued.Envelope.Signal.MessageId).To.Be.Null();
        Expect(dequeued.Envelope.Context).To.Equal(SignalContext.Anonymous);
    }

    [Test]
    public async Task DequeueAsync_WhenContextJsonIsInvalid_ThrowsInsteadOfUsingAnonymousContext()
    {
        // Arrange
        var sut = new SharedSqliteSignalQueueStore(_dbPath);
        var id = "invalid-context";
        var source = "source";
        var payload = "payload";
        var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO signal_queue (id, source_type, payload_json, context_json, status, created_at, retry_count, max_retries)
                  VALUES ($id, $source, $payload, $context, 'pending', $createdAt, 0, 3);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$source", source);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$context", "{");
            command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        // Act & Assert
        Expect(async () => await sut.DequeueAsync())
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("context_json");
    }

    private async Task CreateLegacyQueueRowAsync(
        string payload,
        string source,
        string sessionId,
        DateTimeOffset createdAt)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"CREATE TABLE signal_queue (
                id TEXT PRIMARY KEY,
                session_id TEXT,
                source_type TEXT NOT NULL,
                source_metadata_json TEXT,
                identity_json TEXT,
                payload_json TEXT,
                status TEXT NOT NULL DEFAULT 'pending',
                created_at TEXT NOT NULL,
                picked_at TEXT,
                completed_at TEXT,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3
            );
            INSERT INTO signal_queue (
                id, session_id, source_type, identity_json, payload_json, status, created_at, retry_count, max_retries
            ) VALUES (
                $id, $sessionId, $source, $identity, $payload, 'pending', $createdAt, 0, 3
            );";
        command.Parameters.AddWithValue("$id", "legacy-id");
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$identity", "{}");
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
