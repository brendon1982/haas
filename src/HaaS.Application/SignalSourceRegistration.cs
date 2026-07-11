using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application;

public class SignalSourceRegistration
{
    public SignalSourceRegistration(
        ISignalSource source,
        ISignalPresenter presenter,
        SignalSourceConfig config)
    {
        Source = source;
        Presenter = presenter;
        Config = config;
    }

    public ISignalSource Source { get; }
    public ISignalPresenter Presenter { get; }
    public SignalSourceConfig Config { get; }
    public Guid? LastSessionId { get; set; }
}
