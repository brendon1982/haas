using System.Diagnostics;
using HaaS.Domain.Ports;

using Microsoft.Extensions.Hosting;

namespace HaaS.Adapters.Observability;

public sealed class ObservableHaasEngine : IHaasEngine
{
    private readonly IHaasEngine _inner;
    private readonly ILogger _logger;

    public ObservableHaasEngine(IHaasEngine inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("HaaS Engine starting...");
        return _inner.StartAsync(ct);
    }

    public Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("HaaS Engine stopping...");
        return _inner.StopAsync(ct);
    }
}
