using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class InMemorySignalQueue(TimeProvider? timeProvider = null) : ISignalQueue
{
    private readonly ConcurrentQueue<QueuedSignal> _queue = new();
    private readonly ConcurrentDictionary<string, QueuedSignal> _processing = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task EnqueueAsync(Signal signal, Identity identity)
    {
        var id = Guid.NewGuid().ToString();
        var queued = new QueuedSignal(
            id, signal, identity, SignalStatus.Pending, _timeProvider.GetUtcNow());
        _queue.Enqueue(queued);
        return Task.CompletedTask;
    }

    public Task<QueuedSignal?> DequeueAsync()
    {
        var now = _timeProvider.GetUtcNow();
        int attempts = _queue.Count;
        
        while (attempts-- > 0)
        {
            if (_queue.TryDequeue(out var queued))
            {
                if (queued.VisibleAt == null || queued.VisibleAt <= now)
                {
                    var processing = queued with 
                    { 
                        Status = SignalStatus.Processing, 
                        PickedAt = now 
                    };
                    _processing[processing.Id] = processing;
                    return Task.FromResult<QueuedSignal?>(processing);
                }
                else
                {
                    // Not visible yet, put it back at the end
                    _queue.Enqueue(queued);
                }
            }
        }
        return Task.FromResult<QueuedSignal?>(null);
    }

    public Task AckAsync(string id)
    {
        _processing.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task NackAsync(string id, string? error = null)
    {
        if (_processing.TryRemove(id, out var processing))
        {
            var retryCount = processing.RetryCount + 1;
            var status = retryCount >= processing.MaxRetries ? SignalStatus.Failed : SignalStatus.Pending;
            
            DateTimeOffset? visibleAt = null;
            if (status == SignalStatus.Pending)
            {
                visibleAt = _timeProvider.GetUtcNow().AddSeconds(Math.Pow(2, retryCount));
            }

            var updated = processing with 
            { 
                Status = status, 
                RetryCount = retryCount,
                PickedAt = null,
                VisibleAt = visibleAt,
                LastError = error
            };

            if (status == SignalStatus.Pending)
            {
                _queue.Enqueue(updated);
            }
            // If failed, we don't re-enqueue, but it's kept out of 'processing'
        }
        return Task.CompletedTask;
    }
}
