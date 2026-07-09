using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static IServiceCollection WithInMemoryConfig(
        this IServiceCollection services,
        Action<HaasCliOptions>? configure = null)
    {
        services.AddSingleton<IProviderConfigRepository, InMemoryProviderConfigRepository>();

        var options = new HaasCliOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<ChatClientFactory>(sp =>
        {
            var configRepo = sp.GetRequiredService<IProviderConfigRepository>();
            var factory = new ChatClientFactory(configRepo);
            foreach (var registration in options.Registrations)
                registration(factory, configRepo);
            return factory;
        });

        return services;
    }

    public static IServiceCollection AddSignalSources(this IServiceCollection services)
    {
        services.AddTransient<ISignalSource, CliSignalSource>();
        return services;
    }
}
