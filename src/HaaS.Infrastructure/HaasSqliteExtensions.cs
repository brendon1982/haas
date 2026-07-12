using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public static class HaasSqliteExtensions
{
    public static HaasBuilder WithSqlitePersistence(
        this HaasBuilder builder,
        string sharedDbDirectory)
    {
        var services = builder.Services;
        
        if (!Directory.Exists(sharedDbDirectory))
        {
            Directory.CreateDirectory(sharedDbDirectory);
        }

        var sessionsDbPath = Path.Combine(sharedDbDirectory, "sessions.db");
        var configDbPath = Path.Combine(sharedDbDirectory, "config.db");
        var perSessionDir = Path.Combine(sharedDbDirectory, "sessions");

        // Use SQLite repositories instead of in-memory ones
        services.AddSingleton<ISessionRepository>(new SharedSqliteSessionRepository(sessionsDbPath));
        services.AddSingleton<ISignalSourceConfigRepository>(new SharedSqliteSignalSourceConfigRepository(configDbPath));
        services.AddSingleton<IProviderConfigRepository>(new SharedSqliteProviderConfigRepository(configDbPath));
        services.AddSingleton<IMessageStore>(new PerSessionSqliteMessageStore(perSessionDir));

        return builder;
    }
}
