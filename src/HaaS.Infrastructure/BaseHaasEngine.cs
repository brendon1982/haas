using HaaS.Application;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public abstract class BaseHaasEngine : BackgroundService, IHaasEngine
{
    protected readonly ISignalSourceRegistry Registry;
    protected readonly ISignalSourceConfigRepository ConfigRepository;
    protected readonly IHostApplicationLifetime? Lifetime;
    protected readonly ILogger Logger;

    protected BaseHaasEngine(
        ISignalSourceRegistry registry, 
        ISignalSourceConfigRepository configRepository,
        ILogger logger,
        IHostApplicationLifetime? lifetime = null)
    {
        Registry = registry;
        ConfigRepository = configRepository;
        Logger = logger;
        Lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

        var tasks = registrations.Select(reg => RunSourceAsync(reg, stoppingToken)).ToList();
        
        // Use a TaskCompletionSource to keep the engine running as long as the stoppingToken is not cancelled
        // This prevents the application from exiting if all ListenAsync calls finish (e.g. CLI input ends)
        // but we still want to keep the host alive for other background work or until external termination.
        var tcs = new TaskCompletionSource();
        using var registration = stoppingToken.Register(() => tcs.TrySetResult());

        await Task.WhenAll(tasks.Concat(new[] { tcs.Task }));
    }

    protected abstract IEnumerable<SignalSourceRegistration> GetRelevantRegistrations();
    protected abstract Task<ISignalHandle> ProcessSignalAsync(Signal signal, SignalSourceRegistration reg);

    protected async Task RunSourceAsync(SignalSourceRegistration reg, CancellationToken ct)
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
}
