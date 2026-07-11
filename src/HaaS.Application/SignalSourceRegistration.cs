using HaaS.Domain.Ports;

namespace HaaS.Application;

public class SignalSourceRegistration
{
    public SignalSourceRegistration(
        ISignalSource source,
        ISignalPresenter presenter,
        ISignalSourceConfig config)
    {
        Source = source;
        Presenter = presenter;
        Config = config;
    }

    public ISignalSource Source { get; }
    public ISignalPresenter Presenter { get; }
    public ISignalSourceConfig Config { get; }
    public Guid? LastSessionId { get; set; }
}
