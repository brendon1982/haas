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
    private readonly HaasGovernanceConfiguration _governanceConfiguration;

    internal SignalSourceBuilder(
        IServiceCollection services,
        SignalSourceOptions options,
        HaasGovernanceConfiguration governanceConfiguration)
    {
        _services = services;
        _options = options;
        _governanceConfiguration = governanceConfiguration;
    }

    public HaasBuilder WithQueuedProcessing()
    {
        _options.IsQueued = true;
        return new HaasBuilder(_services, _governanceConfiguration);
    }

    public static implicit operator HaasBuilder(SignalSourceBuilder<TSource, TPresenter> builder)
        => new(builder._services, builder._governanceConfiguration);
}
