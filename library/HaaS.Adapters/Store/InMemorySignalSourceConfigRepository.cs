using HaaS.Application;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class InMemorySignalSourceConfigRepository : ISignalSourceConfigRepository
{
    private readonly ISignalSourceRegistry _registry;

    public InMemorySignalSourceConfigRepository(ISignalSourceRegistry registry)
    {
        _registry = registry;
    }

    public Task<SignalSourceConfig?> GetBySourceTypeAsync(string sourceType)
    {
        var config = _registry.GetBySourceType(sourceType)?.Config;
        return Task.FromResult(config);
    }
}
