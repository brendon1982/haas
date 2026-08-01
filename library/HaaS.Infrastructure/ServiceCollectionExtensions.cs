using HaaS.Adapters.Deferred;
using HaaS.Adapters.Agent;
using HaaS.Adapters.Observability;
using HaaS.Adapters.Persistence;
using HaaS.Adapters.Store;
using HaaS.Application.UseCases;
using HaaS.Application;
using HaaS.Application.Policies;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static HaasBuilder AddHaas(this IServiceCollection services)
    {
        var governanceConfiguration = new HaasGovernanceConfiguration();
        services.AddSingleton(governanceConfiguration);

        services.AddSingleton<ISignalSourceConfigRepository, InMemorySignalSourceConfigRepository>();

        services.AddSingleton<IProviderConfigRepository, InMemoryProviderConfigRepository>();
        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.AddSingleton<IMessageStore, InMemorySessionMessageStore>();
        services.AddSingleton<ISignalQueue, InMemorySignalQueue>();
        services.AddSingleton<IDeferredSessionResultStore, DeferredSessionResultStore>();
        services.AddSingleton<ILogger, ConsoleLogger>();
        services.AddSingleton<ISignalScopeAccessor, SignalScopeAccessor>();
        services.AddScoped<SignalContextScope>();
        services.AddScoped<ISignalContextAccessor>(sp => sp.GetRequiredService<SignalContextScope>());
        services.AddScoped<ISignalContextScope>(sp => sp.GetRequiredService<SignalContextScope>());

        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(sp => sp.GetRequiredService<ChatClientFactory>());

        services.AddSingleton<IToolProvider, ToolProvider>();

        services.AddScoped<IAgentStrategy>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            var sessionRepo = sp.GetRequiredService<ISessionRepository>();
            var messageStore = sp.GetRequiredService<IMessageStore>();
            var toolProvider = sp.GetRequiredService<IToolProvider>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var inner = new MicrosoftAgentFrameworkStrategy(factory, sessionRepo, messageStore, toolProvider, timeProvider);
            var logger = sp.GetRequiredService<ILogger>();
            return new ObservableAgentStrategy(inner, logger);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PolicyOptions>(sp =>
            sp.GetRequiredService<HaasGovernanceConfiguration>().CreateOptions());
        services.AddSingleton<IPolicyRuleRepository>(sp =>
        {
            var configuration = sp.GetRequiredService<HaasGovernanceConfiguration>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            return new InMemoryPolicyRuleRepository(configuration.CreateSeedRules(timeProvider));
        });
        services.AddSingleton<DeterministicPolicyEngine>();
        services.AddSingleton<IPolicyEngine>(sp =>
            sp.GetRequiredService<DeterministicPolicyEngine>());
        services.AddSingleton<ListPolicyRulesUseCase>();
        services.AddSingleton<IListPolicyRulesUseCase>(sp =>
            sp.GetRequiredService<ListPolicyRulesUseCase>());
        services.AddSingleton<GetPolicyRuleUseCase>();
        services.AddSingleton<IGetPolicyRuleUseCase>(sp =>
            sp.GetRequiredService<GetPolicyRuleUseCase>());
        services.AddSingleton<SavePolicyRuleUseCase>();
        services.AddSingleton<ISavePolicyRuleUseCase>(sp =>
            sp.GetRequiredService<SavePolicyRuleUseCase>());
        services.AddSingleton<DeletePolicyRuleUseCase>();
        services.AddSingleton<IDeletePolicyRuleUseCase>(sp =>
            sp.GetRequiredService<DeletePolicyRuleUseCase>());
        services.AddScoped<RunSessionUseCase>();
        services.AddScoped<IRunSessionUseCase>(sp =>
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

        return new HaasBuilder(services, governanceConfiguration);
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
