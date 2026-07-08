namespace HaaS.Host.CLI;

public interface ICliModule
{
    string Name { get; }
    string Description { get; }
    Task RunAsync(CancellationToken ct = default);
}
