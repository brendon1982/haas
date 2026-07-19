using System.Linq;
using HaaS.Adapters.Deferred;
using HaaS.Application;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public readonly struct HaasBuilder
{
    public IServiceCollection Services { get; }

    internal HaasBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public HaasBuilder AddQueuedWorkerPool(int workerCount, Action<HaasQueuedPoolBuilder> configure)
    {
        Services.AddSingleton<IQueuedHaasEngineConfigure>(sp => new QueuedHaasEngineConfigure(workerCount));
        var poolBuilder = new HaasQueuedPoolBuilder(this);
        configure(poolBuilder);
        return this;
    }

    public class HaasQueuedPoolBuilder
    {
        private readonly HaasBuilder _parent;

        public HaasQueuedPoolBuilder(HaasBuilder parent)
        {
            _parent = parent;
        }

        public HaasQueuedPoolBuilder AddSignalSource<TSource, TPresenter>(Action<SignalSourceConfigBuilder>? configure = null)
            where TSource : class, ISignalSource
            where TPresenter : class, ISignalPresenter
        {
            _parent.AddSignalSource<TSource, TPresenter>(config =>
            {
                configure?.Invoke(config);
            }).WithQueuedProcessing();
            
            return this;
        }
    }

    public SignalSourceBuilder<TSource, TPresenter> AddSignalSource<TSource, TPresenter>(Action<SignalSourceConfigBuilder> configure)
        where TSource : class, ISignalSource
        where TPresenter : class, ISignalPresenter
    {
        Services.TryAddTransient<TSource>();
        Services.TryAddTransient<TPresenter>();

        var options = new SignalSourceOptions(typeof(TSource));
        Services.AddSingleton(options);

        Services.AddTransient(sp =>
        {
            var presenter = sp.GetRequiredService<TPresenter>();
            
            var hasPresenterConstructor = typeof(TSource).GetConstructors()
                .Any(c => c.GetParameters().Any(p => p.ParameterType.IsAssignableFrom(typeof(TPresenter)) || p.ParameterType == typeof(ISignalPresenter)));
            
            var source = hasPresenterConstructor 
                ? ActivatorUtilities.CreateInstance<TSource>(sp, presenter)
                : ActivatorUtilities.CreateInstance<TSource>(sp);

            var sourceOptions = sp.GetServices<SignalSourceOptions>()
                .First(o => o.SourceType == typeof(TSource));
            
            var builder = new SignalSourceConfigBuilder(source.Type);
            configure(builder);

            return new SignalSourceRegistration(source, presenter, builder.Build(), sourceOptions.IsQueued);
        });

        return new SignalSourceBuilder<TSource, TPresenter>(Services, options);
    }
}

public interface IQueuedHaasEngineConfigure
{
    void Configure(QueuedHaasEngine engine);
}

internal class QueuedHaasEngineConfigure : IQueuedHaasEngineConfigure
{
    private readonly int _workerCount;

    public QueuedHaasEngineConfigure(int workerCount)
    {
        _workerCount = workerCount;
    }

    public void Configure(QueuedHaasEngine engine)
    {
        engine.SetWorkerCount(_workerCount);
    }
}
