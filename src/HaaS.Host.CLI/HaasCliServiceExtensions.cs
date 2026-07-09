using HaaS.Adapters.Agent;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static IServiceCollection AddHaasCli(
        this IServiceCollection services,
        Action<HaasCliOptions>? configure = null)
    {
        var options = new HaasCliOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddTransient<ISignalSource, CliSignalSource>();
        return services;
    }

    public static async Task InitializeHaasCliAsync(this IServiceProvider services)
    {
        var options = services.GetRequiredService<HaasCliOptions>();
        var factory = services.GetRequiredService<ChatClientFactory>();
        var configRepo = services.GetRequiredService<IProviderConfigRepository>();
        foreach (var registration in options.Registrations)
            await registration(factory, configRepo);
    }
}
