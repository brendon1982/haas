using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface ISignalSourceConfigRepository
{
    Task<SignalSourceConfig?> GetBySourceTypeAsync(string sourceType);
}
