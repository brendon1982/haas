using HaaS.Application.UseCases;
using HaaS.Domain.Exceptions;
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

        SignalSourceRegistration? registration = null;
        try
        {
            _logger.LogInformation("Processing signal {0} for source {1}, SessionId: {2}", 
                queued.Id, queued.Envelope.Signal.Source, queued.Envelope.Signal.SessionId);
            
            registration = _registry.GetBySourceType(queued.Envelope.Signal.Source);
            if (registration == null)
            {
                _logger.LogWarning("No registration found for source type {0}. Nacking signal {1}", 
                    queued.Envelope.Signal.Source, queued.Id);
                await _queue.NackAsync(queued.Id, $"No registration found for source type {queued.Envelope.Signal.Source}");
                return;
            }

            if (queued.Envelope.Signal.SessionId != null)
            {
                await registration.Presenter.PresentProcessingAsync(queued.Envelope.Signal.SessionId, queued.Envelope.Signal.MessageId);
            }

            var result = await _runSessionUseCase.ExecuteAsync(queued.Envelope, registration.Presenter);
            _resultStore.SetResult(result.SessionId, result);
            
            await _queue.AckAsync(queued.Id);
            _logger.LogInformation("Successfully completed signal {0}", queued.Id);
        }
        catch (GovernanceDeniedException ex)
        {
            _logger.LogWarning("Governance denied queued signal.");
            if (registration is not null)
            {
                await registration.Presenter.PresentErrorAsync(ex.SessionId, ex);
            }

            _resultStore.SetError(ex.SessionId, ex);
            await _queue.AckAsync(queued.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process signal {0}. Retry count: {1}. Nacking.", queued.Id, queued.RetryCount);
            if (queued.Envelope.Signal.SessionId != null)
            {
                _resultStore.SetError(queued.Envelope.Signal.SessionId, ex);
            }
            await _queue.NackAsync(queued.Id, ex.Message);
            throw;
        }
    }
}
