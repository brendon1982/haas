using System.Diagnostics;
using HaaS.Domain.Ports;

namespace HaaS.Adapters.Observability;

public sealed class ObservableHaasEngine : IHaasEngine
{
    private static readonly ActivitySource ActivitySource = new("HaaS.Engine");

    private readonly IHaasEngine _inner;
    private readonly ILogger _logger;

    public ObservableHaasEngine(IHaasEngine inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("EngineRun");
        
        _logger.LogInformation("HaaS Engine starting...");
        
        try
        {
            await _inner.RunAsync(ct);
            _logger.LogInformation("HaaS Engine stopped");
        }
        catch (Exception ex)
        {
            activity?.SetTag("error", true);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "HaaS Engine failed");
            throw;
        }
    }
}
