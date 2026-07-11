using HaaS.Application;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public readonly struct HaasBuilder
{
    public IServiceCollection Services { get; }

    internal HaasBuilder(IServiceCollection services)
    {
        Services = services;
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
            var builder = new SignalSourceConfigBuilder(source.Type);
            configure(builder);

            return new SignalSourceRegistration(source, presenter, builder.Build());
        });

        return this;
    }
}
