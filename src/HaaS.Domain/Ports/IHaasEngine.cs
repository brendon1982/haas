namespace HaaS.Domain.Ports;

public interface IHaasEngine
{
    Task RunAsync(CancellationToken ct = default);
}
