using HaaS.Application;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NExpect;
using static NExpect.Expectations;
using NUnit.Framework;

namespace HaaS.Infrastructure.Tests;

[TestFixture]
public class MachineryIntegrationTests
{
    [Test]
    public async Task Signal_PushedThroughManualSource_ReachesPresenterViaEngineAndUseCase()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHaas();
        
        // Replace strategy with capturing one
        var capturingStrategy = new CapturingStrategy();
        services.AddScoped<IAgentStrategy>(_ => capturingStrategy);

        var manualSource = new ManualSignalSource();
        var capturingPresenter = new CapturingPresenter();
        var config = SignalSourceConfigTestBuilder.Create()
            .WithSourceType("manual")
            .Build();
        var registration = new SignalSourceRegistration(manualSource, capturingPresenter, config, isQueued: false);
        services.AddSingleton(registration);

        var sp = services.BuildServiceProvider();

        var engine = sp.GetRequiredService<IHaasEngine>();
        using var cts = new CancellationTokenSource();
        var engineTask = engine.StartAsync(cts.Token);

        // Act
        var signal = new IncomingSignal("Hello Machinery");
        
        // Give the engine a moment to start and call ListenAsync
        await Task.Delay(100); 
        
        var handle = await manualSource.PushAsync(signal);

        // Assert
        Expect(handle).Not.To.Be.Null();
        Expect(capturingStrategy.LastSignal?.Payload).To.Equal("Hello Machinery");
        Expect(capturingPresenter.Results).To.Contain.Exactly(1);
        Expect(capturingPresenter.Results[0].SessionId).To.Equal(handle.SessionId);

        // Cleanup
        cts.Cancel();
        manualSource.Stop();
        await engineTask;
    }
}

// --- Integration Fakes ---

file sealed class ManualSignalSource : ISignalSource
{
    private Func<IncomingSignal, Task<ISignalHandle>>? _callback;
    private readonly TaskCompletionSource _tcs = new();

    public string Type => "manual";

    public Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> onSignalReceived)
    {
        _callback = onSignalReceived;
        return _tcs.Task;
    }

    public Task ShutdownAsync()
    {
        Stop();
        return Task.CompletedTask;
    }

    public void Stop() => _tcs.TrySetResult();

    public async Task<ISignalHandle> PushAsync(IncomingSignal signal)
    {
        if (_callback == null) throw new InvalidOperationException("Source is not listening.");
        return await _callback(signal);
    }
}

file sealed class CapturingStrategy : IAgentStrategy
{
    public Signal? LastSignal { get; private set; }
    public SessionResult ResultToReturn { get; set; } = SessionResultTestBuilder.Create().Build();

    public async Task<SessionResult> ExecuteAsync(Signal signal, string sessionId, ISignalPresenter presenter)
    {
        LastSignal = signal;
        var result = ResultToReturn with { SessionId = sessionId };
        await presenter.PresentAsync(result);
        return result;
    }
}

file sealed class CapturingPresenter : ISignalPresenter
{
    public List<SessionResult> Results { get; } = [];
    public Task PresentAsync(SessionResult result)
    {
        Results.Add(result);
        return Task.CompletedTask;
    }
}
