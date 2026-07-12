using HaaS.Application.UseCases;
using HaaS.Domain.Ports;

namespace HaaS.Application;

public class HaasEngine : IHaasEngine
{
    private readonly ISignalSourceRegistry _registry;
    private readonly IEnqueueSignalUseCase _enqueueSignalUseCase;
    private readonly IRunSessionUseCase _runSessionUseCase;
    private readonly ISignalSourceConfigRepository _configRepository;
    private readonly IDeferredSessionResultStore _resultStore;

    public HaasEngine(
        ISignalSourceRegistry registry,
        IEnqueueSignalUseCase enqueueSignalUseCase,
        IRunSessionUseCase runSessionUseCase,
        ISignalSourceConfigRepository configRepository,
        IDeferredSessionResultStore resultStore)
    {
        _registry = registry;
        _enqueueSignalUseCase = enqueueSignalUseCase;
        _runSessionUseCase = runSessionUseCase;
        _configRepository = configRepository;
        _resultStore = resultStore;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var registrations = _registry.GetAll();
        foreach (var reg in registrations)
        {
            await _configRepository.SaveAsync(reg.Config);
        }

        var tasks = registrations.Select(reg => RunSourceAsync(reg, ct));
        await Task.WhenAll(tasks);
    }

    private async Task RunSourceAsync(SignalSourceRegistration reg, CancellationToken ct)
    {
        await reg.Source.ListenAsync(async signal =>
        {
            if (signal.SessionId == null && reg.LastSessionId.HasValue)
            {
                signal = signal with { SessionId = reg.LastSessionId.Value.ToString() };
            }

            string sessionId;
            ISignalHandle handle;

            if (reg.IsQueued)
            {
                sessionId = await _enqueueSignalUseCase.ExecuteAsync(signal);
                handle = new QueuedSignalHandle(sessionId, _resultStore);
            }
            else
            {
                sessionId = await _runSessionUseCase.ExecuteAsync(signal, reg.Presenter);
                handle = new DirectSignalHandle(sessionId);
            }
            
            if (Guid.TryParse(sessionId, out var guid))
            {
                reg.LastSessionId = guid;
            }

            return handle;
        });
    }
}
