using HaaS.Application.UseCases;
using HaaS.Domain.Ports;

namespace HaaS.Application;

public class SignalWorker
{
    private readonly ISignalQueue _queue;
    private readonly IRunSessionUseCase _runSessionUseCase;
    private readonly ISignalSourceRegistry _registry;
    private readonly IDeferredSessionResultStore _resultStore;
    private readonly ILogger _logger;

    public SignalWorker(
        ISignalQueue queue,
        IRunSessionUseCase runSessionUseCase,
        ISignalSourceRegistry registry,
        IDeferredSessionResultStore resultStore,
        ILogger logger)
    {
        _queue = queue;
        _runSessionUseCase = runSessionUseCase;
        _registry = registry;
        _resultStore = resultStore;
        _logger = logger;
    }

    public async Task ProcessNextAsync(CancellationToken ct)
    {
        var queued = await _queue.DequeueAsync();
        if (queued == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Processing signal {0} for source {1}, SessionId: {2}", 
                queued.Id, queued.Signal.Source, queued.Signal.SessionId);
            
            var registration = _registry.GetBySourceType(queued.Signal.Source);
            if (registration == null)
            {
                _logger.LogWarning("No registration found for source type {0}. Nacking signal {1}", 
                    queued.Signal.Source, queued.Id);
                await _queue.NackAsync(queued.Id);
                return;
            }

            if (queued.Signal.SessionId != null)
            {
                await registration.Presenter.PresentProcessingAsync(queued.Signal.SessionId, queued.Signal.MessageId);
            }

            var result = await _runSessionUseCase.ExecuteAsync(queued.Signal, registration.Presenter);
            _resultStore.SetResult(result.SessionId, result);
            
            await _queue.AckAsync(queued.Id);
            _logger.LogInformation("Successfully completed signal {0}", queued.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process signal {0}. Nacking.", queued.Id);
            if (queued.Signal.SessionId != null)
            {
                _resultStore.SetError(queued.Signal.SessionId, ex);
            }
            await _queue.NackAsync(queued.Id);
            throw;
        }
    }
}
