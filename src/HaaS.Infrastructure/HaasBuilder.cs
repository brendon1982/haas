using System.Linq;
using HaaS.Adapters.Deferred;
using HaaS.Application;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public readonly struct HaasBuilder
{
    public IServiceCollection Services { get; }

    internal HaasBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public HaasBuilder WithWorkerPool(int workerCount)
    {
        Services.AddSingleton<IQueuedHaasEngineConfigure>(sp => new QueuedHaasEngineConfigure(workerCount));
        return this;
    }

    public SignalSourceBuilder AddSignalSource<TSource, TPresenter>(Action<SignalSourceConfigBuilder> configure)
        where TSource : class, ISignalSource
        where TPresenter : class, ISignalPresenter
    {
        Services.AddTransient<TSource>();
        Services.AddTransient<TPresenter>();

        var options = new SignalSourceOptions(typeof(TSource));
        Services.AddSingleton(options);

        Services.AddTransient(sp =>
        {
            var source = sp.GetRequiredService<TSource>();
            var presenter = sp.GetRequiredService<TPresenter>();
            var sourceOptions = sp.GetServices<SignalSourceOptions>()
                .First(o => o.SourceType == typeof(TSource));
            
            var builder = new SignalSourceConfigBuilder(source.Type);
            configure(builder);

            return new SignalSourceRegistration(source, presenter, builder.Build(), sourceOptions.IsQueued);
        });

        return new SignalSourceBuilder(Services, options);
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
