using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static HaasBuilder WithSpectreConsole(this HaasBuilder builder)
    {
        builder.Services.AddSingleton<CliLogSink>();
        builder.Services.AddSingleton<CliLayoutManager>();
        
        // Replace existing ILogger with SpectreLogger
        builder.Services.RemoveAll<ILogger>();
        builder.Services.AddSingleton<ILogger, SpectreLogger>();
        
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
