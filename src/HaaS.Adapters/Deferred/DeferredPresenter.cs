using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Deferred;

public class DeferredPresenter : ISignalPresenter
{
    private readonly ISignalPresenter _inner;
    private readonly IDeferredSessionResultStore _store;

    public DeferredPresenter(ISignalPresenter inner, IDeferredSessionResultStore store)
    {
        _inner = inner;
        _store = store;
    }

    public async Task PresentAsync(SessionResult result)
    {
        await _inner.PresentAsync(result);
        _store.SetResult(result.SessionId, result);
    }
}
