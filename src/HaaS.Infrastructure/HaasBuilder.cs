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
        Services.AddSingleton<IHostedService>(sp => 
            new SignalWorkerService(sp, workerCount));
        return this;
    }

    public HaasBuilder AddSignalSource<TSource, TPresenter>(Action<SignalSourceConfigBuilder> configure)
        where TSource : class, ISignalSource
        where TPresenter : class, ISignalPresenter
    {
        Services.AddTransient<TSource>();
        Services.AddTransient<TPresenter>();

        Services.AddTransient(sp =>
        {
            var source = sp.GetRequiredService<TSource>();
            var presenter = sp.GetRequiredService<TPresenter>();
            var resultStore = sp.GetRequiredService<IDeferredSessionResultStore>();
            
            var deferredPresenter = new DeferredPresenter(presenter, resultStore);
            
            var builder = new SignalSourceConfigBuilder(source.Type);
            configure(builder);

            return new SignalSourceRegistration(source, deferredPresenter, builder.Build());
        });

        return this;
    }
}
