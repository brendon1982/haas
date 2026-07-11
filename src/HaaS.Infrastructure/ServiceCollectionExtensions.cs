using HaaS.Adapters.Agent;
using HaaS.Adapters.Observability;
using HaaS.Adapters.Persistence;
using HaaS.Adapters.Store;
using HaaS.Application.UseCases;
using HaaS.Application;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static HaasBuilder AddHaas(this IServiceCollection services)
    {
        services.AddSingleton<ISignalSourceConfigRepository, InMemorySignalSourceConfigRepository>();
        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.AddSingleton<IMessageStore, InMemorySessionMessageStore>();
        services.AddSingleton<ILogger, ConsoleLogger>();

        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(sp => sp.GetRequiredService<ChatClientFactory>());

        services.AddSingleton<IToolRegistry, ToolRegistry>();

        services.AddSingleton<IAgentStrategy>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            var sessionRepo = sp.GetRequiredService<ISessionRepository>();
            var messageStore = sp.GetRequiredService<IMessageStore>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var inner = new MicrosoftAgentFrameworkStrategy(factory, sessionRepo, messageStore, toolRegistry);
            var logger = sp.GetRequiredService<ILogger>();
            return new ObservableAgentStrategy(inner, logger);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RunSessionUseCase>();
        services.AddSingleton<IHaasEngine, HaasEngine>();

        return new HaasBuilder(services);
    }
}
