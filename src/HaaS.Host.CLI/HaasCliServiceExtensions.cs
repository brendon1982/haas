using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static HaasBuilder WithSpectreConsole(this HaasBuilder builder, CliLayoutManager? layoutManager = null)
    {
        builder.Services.AddSingleton<CliLogSink>();
        if (layoutManager != null)
        {
            builder.Services.AddSingleton<CliLayoutManager>(sp => layoutManager);
        }
        else
        {
            builder.Services.AddSingleton<CliLayoutManager>();
        }
        builder.Services.AddSingleton<CliSignalPresenter>();
        
        // Replace existing ILogger with SpectreLogger
        builder.Services.RemoveAll<HaaS.Domain.Ports.ILogger>();
        builder.Services.AddSingleton<HaaS.Domain.Ports.ILogger, SpectreLogger>();

        // Redirect Microsoft.Extensions.Logging to CliLogSink
        builder.Services.AddLogging(logging => logging.ClearProviders());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SpectreLoggingProvider>());
        
        return builder;
    }

    public static HaasBuilder WithInMemoryConfig(
        this HaasBuilder builder,
        Action<HaasInMemoryConfig>? configure = null)
    {
        var services = builder.Services;
        var config = new HaasInMemoryConfig();
        configure?.Invoke(config);
        services.AddSingleton(config);
        services.AddSingleton<IProviderConfigRepository>(
            new InMemoryProviderConfigRepository(config.ProviderConfigs));
        services.AddSingleton<ChatClientFactory>(sp =>
        {
            var configRepo = sp.GetRequiredService<IProviderConfigRepository>();
            var factory = new ChatClientFactory(configRepo);
            foreach (var register in config.FactoryRegistrations)
                register(factory);
            return factory;
        });
        return builder;
    }
}
