using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class InMemorySignalQueue : ISignalQueue
{
    private readonly ConcurrentQueue<QueuedSignal> _queue = new();
    private readonly ConcurrentDictionary<string, QueuedSignal> _processing = new();

    public Task EnqueueAsync(Signal signal, Identity identity)
    {
        var id = Guid.NewGuid().ToString();
        var queued = new QueuedSignal(
            id, signal, identity, SignalStatus.Pending, DateTimeOffset.UtcNow);
        _queue.Enqueue(queued);
        return Task.CompletedTask;
    }

    public Task<QueuedSignal?> DequeueAsync()
    {
        if (_queue.TryDequeue(out var queued))
        {
            var processing = queued with 
            { 
                Status = SignalStatus.Processing, 
                PickedAt = DateTimeOffset.UtcNow 
            };
            _processing[processing.Id] = processing;
            return Task.FromResult<QueuedSignal?>(processing);
        }
        return Task.FromResult<QueuedSignal?>(null);
    }

    public Task AckAsync(string id)
    {
        _processing.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task NackAsync(string id)
    {
        if (_processing.TryRemove(id, out var processing))
        {
            var pending = processing with 
            { 
                Status = SignalStatus.Pending, 
                RetryCount = processing.RetryCount + 1,
                PickedAt = null
            };
            _queue.Enqueue(pending);
        }
        return Task.CompletedTask;
    }
}
