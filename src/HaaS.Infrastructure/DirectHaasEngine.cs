using HaaS.Application;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Infrastructure;

public class DirectHaasEngine : BaseHaasEngine
{
    private readonly IRunSessionUseCase _runSessionUseCase;

    public DirectHaasEngine(
        ISignalSourceRegistry registry, 
        ISignalSourceConfigRepository configRepository,
        IRunSessionUseCase runSessionUseCase,
        ILogger logger)
        : base(registry, configRepository, logger)
    {
        _runSessionUseCase = runSessionUseCase;
    }

    protected override IEnumerable<SignalSourceRegistration> GetRelevantRegistrations()
        => Registry.GetAll().Where(r => !r.IsQueued);

    protected override async Task<ISignalHandle> ProcessSignalAsync(Signal signal, SignalSourceRegistration reg)
    {
        var result = await _runSessionUseCase.ExecuteAsync(signal, reg.Presenter);
        return new DirectSignalHandle(result.SessionId);
    }
}

file sealed class DirectSignalHandle : ISignalHandle
{
    public string SessionId { get; }
    public DirectSignalHandle(string sessionId) => SessionId = sessionId;
    public Task WaitForResultAsync(CancellationToken ct = default) => Task.CompletedTask;
}
