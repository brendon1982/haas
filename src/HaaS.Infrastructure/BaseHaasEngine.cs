using HaaS.Application;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public abstract class BaseHaasEngine : BackgroundService, IHaasEngine
{
    protected readonly ISignalSourceRegistry Registry;
    protected readonly ISignalSourceConfigRepository ConfigRepository;
    protected readonly ILogger Logger;

    protected BaseHaasEngine(
        ISignalSourceRegistry registry, 
        ISignalSourceConfigRepository configRepository,
        ILogger logger)
    {
        Registry = registry;
        ConfigRepository = configRepository;
        Logger = logger;
    }

    private Task? _executingTask;

    public Task RunAsync(CancellationToken ct = default)
    {
        lock (this)
        {
            if (_executingTask != null)
            {
                return _executingTask;
            }

            _executingTask = RunInternalAsync(ct);
            return _executingTask;
        }
    }

    private async Task RunInternalAsync(CancellationToken ct)
    {
        var registrations = GetRelevantRegistrations().ToList();
        if (!registrations.Any())
        {
            return;
        }

        foreach (var reg in registrations)
        {
            await ConfigRepository.SaveAsync(reg.Config);
        }

        var tasks = registrations.Select(reg => RunSourceAsync(reg, ct));
        await Task.WhenAll(tasks);
    }

    protected abstract IEnumerable<SignalSourceRegistration> GetRelevantRegistrations();
    protected abstract Task<ISignalHandle> ProcessSignalAsync(Signal signal, SignalSourceRegistration reg);

    private async Task RunSourceAsync(SignalSourceRegistration reg, CancellationToken ct)
    {
        try 
        {
            await reg.Source.ListenAsync(async signal =>
            {
                if (signal.SessionId == null && reg.LastSessionId.HasValue)
                {
                    signal = signal with { SessionId = reg.LastSessionId.Value.ToString() };
                }

                var handle = await ProcessSignalAsync(signal, reg);
                
                if (Guid.TryParse(handle.SessionId, out var guid))
                {
                    reg.LastSessionId = guid;
                }

                return handle;
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in signal source {0}", reg.Source.Type);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);
}
