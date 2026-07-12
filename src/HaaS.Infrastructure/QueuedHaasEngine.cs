using HaaS.Application;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public class QueuedHaasEngine : BaseHaasEngine
{
    private readonly IEnqueueSignalUseCase _enqueueSignalUseCase;
    private readonly IDeferredSessionResultStore _resultStore;
    private readonly IServiceProvider _serviceProvider;
    private int _workerCount = 1;

    public QueuedHaasEngine(
        ISignalSourceRegistry registry, 
        ISignalSourceConfigRepository configRepository,
        IEnqueueSignalUseCase enqueueSignalUseCase,
        IDeferredSessionResultStore resultStore,
        IServiceProvider serviceProvider,
        ILogger logger,
        IHostApplicationLifetime? lifetime = null)
        : base(registry, configRepository, logger, lifetime)
    {
        _enqueueSignalUseCase = enqueueSignalUseCase;
        _resultStore = resultStore;
        _serviceProvider = serviceProvider;
    }

    public void SetWorkerCount(int workerCount)
    {
        _workerCount = workerCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var registrations = GetRelevantRegistrations().ToList();
        
        // Always save configs for relevant sources
        foreach (var reg in registrations)
        {
            await ConfigRepository.SaveAsync(reg.Config);
        }

        var sourceTasks = registrations.Select(reg => RunSourceAsync(reg, stoppingToken));
        var workerTasks = Enumerable.Range(0, _workerCount).Select(_ => RunWorkerAsync(stoppingToken));

        await Task.WhenAll(sourceTasks.Concat(workerTasks));
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var worker = scope.ServiceProvider.GetRequiredService<SignalWorker>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await worker.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in signal worker");
            }

            await Task.Delay(50, stoppingToken);
        }
    }

    protected override IEnumerable<SignalSourceRegistration> GetRelevantRegistrations()
        => Registry.GetAll().Where(r => r.IsQueued);

    protected override async Task<ISignalHandle> ProcessSignalAsync(Signal signal, SignalSourceRegistration reg)
    {
        var sessionId = await _enqueueSignalUseCase.ExecuteAsync(signal);
        return new QueuedSignalHandle(sessionId, _resultStore);
    }
}

file sealed class QueuedSignalHandle : ISignalHandle
{
    private readonly IDeferredSessionResultStore _store;
    public string SessionId { get; }

    public QueuedSignalHandle(string sessionId, IDeferredSessionResultStore store)
    {
        SessionId = sessionId;
        _store = store;
    }

    public Task WaitForResultAsync(CancellationToken ct = default)
        => _store.WaitForResultAsync(SessionId, ct);
}
