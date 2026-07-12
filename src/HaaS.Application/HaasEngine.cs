using HaaS.Application.UseCases;
using HaaS.Domain.Ports;

namespace HaaS.Application;

public class HaasEngine : IHaasEngine
{
    private readonly IEnumerable<SignalSourceRegistration> _registrations;
    private readonly IRunSessionUseCase _runSessionUseCase;
    private readonly ISignalSourceConfigRepository _configRepository;

    public HaasEngine(
        IEnumerable<SignalSourceRegistration> registrations,
        IRunSessionUseCase runSessionUseCase,
        ISignalSourceConfigRepository configRepository)
    {
        _registrations = registrations;
        _runSessionUseCase = runSessionUseCase;
        _configRepository = configRepository;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        foreach (var reg in _registrations)
        {
            await _configRepository.SaveAsync(reg.Config);
        }

        var tasks = _registrations.Select(reg => RunSourceAsync(reg, ct));
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

            var sessionId = await _runSessionUseCase.ExecuteAsync(signal, reg.Presenter);
            
            if (Guid.TryParse(sessionId, out var guid))
            {
                reg.LastSessionId = guid;
            }
        });
    }
}
