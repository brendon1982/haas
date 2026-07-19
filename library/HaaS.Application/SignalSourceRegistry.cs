using System.Collections.Concurrent;

namespace HaaS.Application;

public interface ISignalSourceRegistry
{
    void Register(SignalSourceRegistration registration);
    SignalSourceRegistration? GetBySourceType(string sourceType);
    IEnumerable<SignalSourceRegistration> GetAll();
}

public class SignalSourceRegistry : ISignalSourceRegistry
{
    private readonly ConcurrentDictionary<string, SignalSourceRegistration> _registrations = new();

    public void Register(SignalSourceRegistration registration)
    {
        _registrations[registration.Source.Type] = registration;
    }

    public SignalSourceRegistration? GetBySourceType(string sourceType)
    {
        _registrations.TryGetValue(sourceType, out var registration);
        return registration;
    }

    public IEnumerable<SignalSourceRegistration> GetAll() => _registrations.Values;
}
