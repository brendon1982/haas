using HaaS.Application;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Infrastructure;

public class QueuedHaasEngine : BaseHaasEngine
{
    private readonly IEnqueueSignalUseCase _enqueueSignalUseCase;
    private readonly IDeferredSessionResultStore _resultStore;

    public QueuedHaasEngine(
        ISignalSourceRegistry registry, 
        ISignalSourceConfigRepository configRepository,
        IEnqueueSignalUseCase enqueueSignalUseCase,
        IDeferredSessionResultStore resultStore,
        ILogger logger)
        : base(registry, configRepository, logger)
    {
        _enqueueSignalUseCase = enqueueSignalUseCase;
        _resultStore = resultStore;
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
