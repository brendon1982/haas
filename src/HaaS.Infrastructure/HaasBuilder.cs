using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Infrastructure;

public readonly struct HaasBuilder
{
    public IServiceCollection Services { get; }

    internal HaasBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
