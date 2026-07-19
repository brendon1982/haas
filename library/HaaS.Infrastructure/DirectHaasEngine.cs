using HaaS.Application;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public class DirectHaasEngine : BaseHaasEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISignalScopeAccessor _scopeAccessor;

    public DirectHaasEngine(
        ISignalSourceRegistry registry, 
        IServiceScopeFactory scopeFactory,
        ISignalScopeAccessor scopeAccessor,
        ILogger logger,
        IHostApplicationLifetime? lifetime = null)
        : base(registry, logger, lifetime)
    {
        _scopeFactory = scopeFactory;
        _scopeAccessor = scopeAccessor;
    }

    protected override IEnumerable<SignalSourceRegistration> GetRelevantRegistrations()
        => Registry.GetAll().Where(r => !r.IsQueued);

    protected override async Task<ISignalHandle> ExecuteProcessSignalAsync(Signal signal, SignalSourceRegistration reg)
    {
        using var scope = _scopeFactory.CreateScope();
        try
        {
            _scopeAccessor.ServiceProvider = scope.ServiceProvider;
            var runSessionUseCase = scope.ServiceProvider.GetRequiredService<IRunSessionUseCase>();
            var result = await runSessionUseCase.ExecuteAsync(signal, reg.Presenter);
            return new DirectSignalHandle(result.SessionId);
        }
        finally
        {
            _scopeAccessor.ServiceProvider = null;
        }
    }
}

file sealed class DirectSignalHandle : ISignalHandle
{
    public string SessionId { get; }
    public DirectSignalHandle(string sessionId) => SessionId = sessionId;
    public Task WaitForResultAsync(CancellationToken ct = default) => Task.CompletedTask;
}
