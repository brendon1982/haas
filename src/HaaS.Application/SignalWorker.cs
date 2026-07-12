using HaaS.Application.UseCases;
using HaaS.Domain.Ports;

namespace HaaS.Application;

public class SignalWorker
{
    private readonly ISignalQueue _queue;
    private readonly IRunSessionUseCase _runSessionUseCase;
    private readonly ISignalSourceRegistry _registry;
    private readonly ILogger _logger;

    public SignalWorker(
        ISignalQueue queue,
        IRunSessionUseCase runSessionUseCase,
        ISignalSourceRegistry registry,
        ILogger logger)
    {
        _queue = queue;
        _runSessionUseCase = runSessionUseCase;
        _registry = registry;
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
            _logger.LogInformation("Processing signal {Id} for source {Source}, SessionId: {SessionId}", 
                queued.Id, queued.Signal.Source, queued.Signal.SessionId);
            
            var registration = _registry.GetBySourceType(queued.Signal.Source);
            if (registration == null)
            {
                _logger.LogWarning("No registration found for source type {Source}. Nacking signal {Id}", 
                    queued.Signal.Source, queued.Id);
                await _queue.NackAsync(queued.Id);
                return;
            }

            await _runSessionUseCase.ExecuteAsync(queued.Signal, registration.Presenter);
            await _queue.AckAsync(queued.Id);
            _logger.LogInformation("Successfully completed signal {Id}", queued.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process signal {Id}. Nacking.", queued.Id);
            await _queue.NackAsync(queued.Id);
            throw;
        }
    }
}
