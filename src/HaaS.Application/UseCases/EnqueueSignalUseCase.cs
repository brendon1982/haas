using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public class EnqueueSignalUseCase : IEnqueueSignalUseCase
{
    private readonly ISignalQueue _queue;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public EnqueueSignalUseCase(ISignalQueue queue, TimeProvider timeProvider, ILogger logger)
    {
        _queue = queue;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Signal signal)
    {
        var sessionId = signal.SessionId ?? Guid.NewGuid().ToString();
        var signalWithMetadata = signal with 
        { 
            SessionId = sessionId,
            ArrivedAt = _timeProvider.GetUtcNow()
        };

        await _queue.EnqueueAsync(signalWithMetadata, Identity.Anonymous);
        _logger.LogInformation("Enqueued signal for source {0}, SessionId: {1}", signal.Source, sessionId);

        return sessionId;
    }
}
