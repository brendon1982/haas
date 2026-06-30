using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IProviderConfigRepository
{
    Task<IReadOnlyList<ProviderConfig>> GetAllAsync();
    Task<ProviderConfig?> GetAsync(string provider);
    Task SaveAsync(ProviderConfig config);
}
