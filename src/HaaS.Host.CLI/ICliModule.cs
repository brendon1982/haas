using HaaS.Host.CLI.Infrastructure;

namespace HaaS.Host.CLI;

public interface ICliModule
{
    string Name { get; }
    string Description { get; }
    Task RunAsync(GuiLayoutManager layout, CancellationToken ct = default);
}
