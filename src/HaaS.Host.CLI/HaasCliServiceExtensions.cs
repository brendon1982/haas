using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static IServiceCollection WithInMemoryConfig(
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
        return services;
    }
}
