using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NExpect;
using static NExpect.Expectations;
using NUnit.Framework;

namespace HaaS.Infrastructure.Tests;

[TestFixture]
public class PersistenceVerificationTests
{
    [Test]
    public async Task WithSqlitePersistence_ShouldRegisterAndInitializeRepositories()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var services = new ServiceCollection();
        
        // Act
        services.AddHaas()
            .WithSqlitePersistence(tempDir);
        
        var sp = services.BuildServiceProvider();
        var sessionRepo = sp.GetRequiredService<ISessionRepository>();
        var configRepo = sp.GetRequiredService<ISignalSourceConfigRepository>();
        var messageStore = sp.GetRequiredService<IMessageStore>();

        // Assert
        Expect(sessionRepo).To.Be.An.Instance.Of<HaaS.Adapters.Store.SharedSqliteSessionRepository>();
        Expect(configRepo).To.Be.An.Instance.Of<HaaS.Adapters.Store.SharedSqliteSignalSourceConfigRepository>();
        Expect(messageStore).To.Be.An.Instance.Of<HaaS.Adapters.Store.PerSessionSqliteMessageStore>();

        Expect(File.Exists(Path.Combine(tempDir, "sessions.db"))).To.Be.True();
        Expect(File.Exists(Path.Combine(tempDir, "config.db"))).To.Be.True();
        Expect(Directory.Exists(Path.Combine(tempDir, "sessions"))).To.Be.True();

        // Cleanup
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(tempDir, true);
    }
}
