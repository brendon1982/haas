using System;
using HaaS.Application;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public readonly struct SignalSourceBuilder<TSource, TPresenter>
    where TSource : class, ISignalSource
    where TPresenter : class, ISignalPresenter
{
    private readonly IServiceCollection _services;
    private readonly SignalSourceOptions _options;

    internal SignalSourceBuilder(IServiceCollection services, SignalSourceOptions options)
    {
        _services = services;
        _options = options;
    }

    public HaasBuilder WithQueuedProcessing()
    {
        _options.IsQueued = true;
        return new HaasBuilder(_services);
    }

    public static implicit operator HaasBuilder(SignalSourceBuilder<TSource, TPresenter> builder) => new HaasBuilder(builder._services);
}
