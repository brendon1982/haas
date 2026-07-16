namespace HaaS.Domain.Ports;

public interface ISignalScopeAccessor
{
    IServiceProvider? ServiceProvider { get; set; }
}
