using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public static class HaasSqliteExtensions
{
    public static HaasBuilder WithSqlitePersistence(
        this HaasBuilder builder,
        string sharedDbDirectory,
        bool includeConfig = true)
    {
        builder.WithSqliteSessionRepository(sharedDbDirectory)
               .WithSqliteMessageStore(sharedDbDirectory)
               .WithSqliteQueue(sharedDbDirectory);

        if (includeConfig)
        {
            builder.WithSqliteConfig(sharedDbDirectory);
        }

        return builder;
    }

    public static HaasBuilder WithSqliteSessionRepository(this HaasBuilder builder, string sharedDbDirectory)
    {
        var dbPath = Path.Combine(sharedDbDirectory, "sessions.db");
        EnsureDirectory(sharedDbDirectory);
        builder.Services.AddSingleton<ISessionRepository>(new SharedSqliteSessionRepository(dbPath));
        return builder;
    }

    public static HaasBuilder WithSqliteConfig(this HaasBuilder builder, string sharedDbDirectory)
    {
        var dbPath = Path.Combine(sharedDbDirectory, "config.db");
        EnsureDirectory(sharedDbDirectory);
        builder.Services.AddSingleton<ISignalSourceConfigRepository>(new SharedSqliteSignalSourceConfigRepository(dbPath));
        builder.Services.AddSingleton<IProviderConfigRepository>(new SharedSqliteProviderConfigRepository(dbPath));
        return builder;
    }

    public static HaasBuilder WithSqliteMessageStore(this HaasBuilder builder, string sharedDbDirectory)
    {
        var perSessionDir = Path.Combine(sharedDbDirectory, "sessions");
        EnsureDirectory(sharedDbDirectory);
        builder.Services.AddSingleton<IMessageStore>(new PerSessionSqliteMessageStore(perSessionDir));
        return builder;
    }

    public static HaasBuilder WithSqliteQueue(this HaasBuilder builder, string sharedDbDirectory)
    {
        var dbPath = Path.Combine(sharedDbDirectory, "signal_queue.db");
        EnsureDirectory(sharedDbDirectory);
        builder.Services.AddSingleton<ISignalQueue>(new SharedSqliteSignalQueueStore(dbPath));
        return builder;
    }

    private static void EnsureDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
