using HaaS.Domain.Ports;

namespace HaaS.Infrastructure;

public class SignalScopeAccessor : ISignalScopeAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> _current = new();

    public IServiceProvider? ServiceProvider
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
