using System.Threading;
using System.Threading.Tasks;
using HaaS.Domain.Ports;

namespace HaaS.Application;

internal class DirectSignalHandle : ISignalHandle
{
    public DirectSignalHandle(string sessionId)
    {
        SessionId = sessionId;
    }

    public string SessionId { get; }
    public Task WaitForResultAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal class QueuedSignalHandle : ISignalHandle
{
    private readonly IDeferredSessionResultStore _store;

    public QueuedSignalHandle(string sessionId, IDeferredSessionResultStore store)
    {
        SessionId = sessionId;
        _store = store;
    }

    public string SessionId { get; }
    public Task WaitForResultAsync(CancellationToken ct = default) => _store.WaitForResultAsync(SessionId, ct);
}
