using HaaS.Adapters.Agent;
using HaaS.Adapters.Execution;
using HaaS.Adapters.Observability;
using HaaS.Adapters.Persistence;
using HaaS.Adapters.Signal;
using HaaS.Adapters.Store;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHaasCore(this IServiceCollection services)
    {
        services.AddSingleton<ISignalSourceConfigRepository, InMemorySignalSourceConfigRepository>();
        services.AddSingleton<IProviderConfigRepository, InMemoryProviderConfigRepository>();
        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.AddSingleton<IMessageStore, InMemorySessionMessageStore>();
        services.AddSingleton<ILogger, ConsoleLogger>();

        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(sp => sp.GetRequiredService<ChatClientFactory>());

        services.AddSingleton<IAgentStrategy>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            var sessionRepo = sp.GetRequiredService<ISessionRepository>();
            var messageStore = sp.GetRequiredService<IMessageStore>();
            var inner = new MicrosoftAgentFrameworkStrategy(factory, sessionRepo, messageStore);
            var logger = sp.GetRequiredService<ILogger>();
            return new ObservableAgentStrategy(inner, logger);
        });

        services.AddSingleton<IExecutionTarget, ConsoleExecutionTarget>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RunSessionUseCase>();
        services.AddTransient<ISignalSource, CliSignalSource>();

        return services;
    }
}
