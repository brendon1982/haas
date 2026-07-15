using HaaS.Adapters.Deferred;
using HaaS.Adapters.Agent;
using HaaS.Adapters.Observability;
using HaaS.Adapters.Persistence;
using HaaS.Adapters.Store;
using HaaS.Application.UseCases;
using HaaS.Application;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static HaasBuilder AddHaas(this IServiceCollection services)
    {
        services.AddSingleton<ISignalSourceConfigRepository, InMemorySignalSourceConfigRepository>();

        services.AddSingleton<IProviderConfigRepository, InMemoryProviderConfigRepository>();
        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.AddSingleton<IMessageStore, InMemorySessionMessageStore>();
        services.AddSingleton<ISignalQueue, InMemorySignalQueue>();
        services.AddSingleton<IDeferredSessionResultStore, DeferredSessionResultStore>();
        services.AddSingleton<ILogger, ConsoleLogger>();

        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(sp => sp.GetRequiredService<ChatClientFactory>());

        services.AddSingleton<IToolProvider, ToolProvider>();

        services.AddSingleton<IAgentStrategy>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            var sessionRepo = sp.GetRequiredService<ISessionRepository>();
            var messageStore = sp.GetRequiredService<IMessageStore>();
            var toolProvider = sp.GetRequiredService<IToolProvider>();
            var inner = new MicrosoftAgentFrameworkStrategy(factory, sessionRepo, messageStore, toolProvider);
            var logger = sp.GetRequiredService<ILogger>();
            return new ObservableAgentStrategy(inner, logger);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RunSessionUseCase>();
        services.AddSingleton<IRunSessionUseCase>(sp =>
        {
            var inner = sp.GetRequiredService<RunSessionUseCase>();
            var logger = sp.GetRequiredService<ILogger>();
            return new ObservableRunSessionUseCase(inner, logger);
        });

        services.AddSingleton<EnqueueSignalUseCase>();
        services.AddSingleton<IEnqueueSignalUseCase>(sp => sp.GetRequiredService<EnqueueSignalUseCase>());

        services.AddSingleton<ISignalSourceRegistry, SignalSourceRegistry>();

        services.AddTransient<SignalWorker>();
        
        services.AddSingleton<DirectHaasEngine>();
        services.AddSingleton<QueuedHaasEngine>();
        
        services.AddSingleton<IHaasEngine>(sp =>
        {
            var registry = sp.GetRequiredService<ISignalSourceRegistry>();
            foreach (var reg in sp.GetServices<SignalSourceRegistration>())
            {
                registry.Register(reg);
            }
            
            var direct = sp.GetRequiredService<DirectHaasEngine>();
            var queued = sp.GetRequiredService<QueuedHaasEngine>();
            
            var configs = sp.GetServices<IQueuedHaasEngineConfigure>();
            foreach (var config in configs)
            {
                config.Configure(queued);
            }
            
            var logger = sp.GetRequiredService<ILogger>();
            
            var composite = new CompositeHaasEngine(direct, queued);
            return new ObservableHaasEngine(composite, logger);
        });

        services.AddHostedService<ObservableHaasEngine>(sp => (ObservableHaasEngine)sp.GetRequiredService<IHaasEngine>());

        return new HaasBuilder(services);
    }
}

internal class CompositeHaasEngine : IHaasEngine
{
    private readonly DirectHaasEngine _direct;
    private readonly QueuedHaasEngine _queued;

    public CompositeHaasEngine(DirectHaasEngine direct, QueuedHaasEngine queued)
    {
        _direct = direct;
        _queued = queued;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_direct.StartAsync(cancellationToken), _queued.StartAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_direct.StopAsync(cancellationToken), _queued.StopAsync(cancellationToken));
    }
}
