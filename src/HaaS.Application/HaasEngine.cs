using HaaS.Application.UseCases;
using HaaS.Domain.Ports;

namespace HaaS.Application;

public class HaasEngine : IHaasEngine
{
    private readonly ISignalSourceRegistry _registry;
    private readonly IEnqueueSignalUseCase _enqueueSignalUseCase;
    private readonly ISignalSourceConfigRepository _configRepository;

    public HaasEngine(
        ISignalSourceRegistry registry,
        IEnqueueSignalUseCase enqueueSignalUseCase,
        ISignalSourceConfigRepository configRepository)
    {
        _registry = registry;
        _enqueueSignalUseCase = enqueueSignalUseCase;
        _configRepository = configRepository;
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

            var sessionId = await _enqueueSignalUseCase.ExecuteAsync(signal);
            
            if (Guid.TryParse(sessionId, out var guid))
            {
                reg.LastSessionId = guid;
            }

            return sessionId;
        });
    }
}
