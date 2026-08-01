using HaaS.Application.UseCases;
using HaaS.Application;
using HaaS.Domain.Exceptions;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Infrastructure.Tests;

[TestFixture]
public class DirectHaasEngineTests
{
    [Test]
    public async Task ProcessSignalAsync_PassesTheExactSourceContextToRunSession()
    {
        // Arrange
        var expectedContext = SignalContextTestBuilder.Create()
            .WithAuthentication(AuthenticationContextTestBuilder.Create()
                .WithIdentity(IdentityTestBuilder.Create()
                    .WithIssuer("issuer")
                    .WithSubject("subject")
                    .WithClaim("role", "operator")
                    .Build())
                .WithAuthenticationMethod("oauth")
                .WithCredentialReference("calendar", "vault", "calendar-ref")
                .Build())
            .WithAttribute("tenant", "contoso")
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create().WithSource("source").Build())
            .WithContext(expectedContext)
            .Build();
        var runSession = new CapturingRunSessionUseCase();
        var services = new ServiceCollection();
        services.AddScoped<IRunSessionUseCase>(_ => runSession);
        using var provider = services.BuildServiceProvider();
        var engine = new DirectHaasEngine(
            new FakeSignalSourceRegistry(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeSignalScopeAccessor(),
            new FakeLogger());
        var registration = new SignalSourceRegistration(
            new FakeSignalSource(),
            new FakeSignalPresenter(),
            SignalSourceConfigTestBuilder.Create().WithSourceType("source").Build());

        // Act
        await engine.ProcessSignalAsync(envelope, registration);

        // Assert
        Expect(runSession.ReceivedEnvelope?.Context).To.Equal(expectedContext);
    }

    [Test]
    public async Task ProcessSignalAsync_WhenGovernanceIsDenied_PresentsOneError()
    {
        // Arrange
        var sessionId = "sess-direct-denied";
        var expectedError = new GovernanceDeniedException(
            sessionId,
            "SessionStart",
            "matched-deny-rule");
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create()
                .WithSource("source")
                .WithSessionId(sessionId)
                .Build())
            .Build();
        var runSession = new CapturingRunSessionUseCase { Error = expectedError };
        var services = new ServiceCollection();
        services.AddScoped<IRunSessionUseCase>(_ => runSession);
        using var provider = services.BuildServiceProvider();
        var engine = new DirectHaasEngine(
            new FakeSignalSourceRegistry(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeSignalScopeAccessor(),
            new FakeLogger());
        var presenter = new FakeSignalPresenter();
        var registration = new SignalSourceRegistration(
            new FakeSignalSource(),
            presenter,
            SignalSourceConfigTestBuilder.Create().WithSourceType("source").Build());

        // Act & Assert
        Expect(async () => await engine.ProcessSignalAsync(envelope, registration))
            .To.Throw<GovernanceDeniedException>();
        Expect(presenter.Errors).To.Contain.Exactly(1);
        Expect(presenter.Errors[0].SessionId).To.Equal(sessionId);
        Expect(presenter.Errors[0].Exception).To.Equal(expectedError);
    }
}

file sealed class CapturingRunSessionUseCase : IRunSessionUseCase
{
    public SignalEnvelope? ReceivedEnvelope { get; private set; }
    public Exception? Error { get; init; }

    public Task<SessionResult> ExecuteAsync(SignalEnvelope envelope, ISignalPresenter presenter)
    {
        ReceivedEnvelope = envelope;
        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(SessionResultTestBuilder.Create().WithSessionId("session-42").Build());
    }
}

file sealed class FakeSignalScopeAccessor : ISignalScopeAccessor
{
    public IServiceProvider? ServiceProvider { get; set; }
}

file sealed class FakeSignalSourceRegistry : ISignalSourceRegistry
{
    public SignalSourceRegistration? GetBySourceType(string sourceType) => null;
    public IEnumerable<SignalSourceRegistration> GetAll() => [];
    public void Register(SignalSourceRegistration registration) { }
}

file sealed class FakeSignalSource : ISignalSource
{
    public string Type => "source";
    public Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler) => Task.CompletedTask;
    public Task ShutdownAsync() => Task.CompletedTask;
}

file sealed class FakeSignalPresenter : ISignalPresenter
{
    public List<(string? SessionId, Exception Exception)> Errors { get; } = [];

    public Task PresentAsync(SessionResult result) => Task.CompletedTask;

    public Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        Errors.Add((sessionId, exception));
        return Task.CompletedTask;
    }

    public Task PresentProcessingAsync(string sessionId, string? messageId = null) => Task.CompletedTask;
}

file sealed class FakeLogger : ILogger
{
    public void LogTrace(string message, params object?[] args) { }
    public void LogDebug(string message, params object?[] args) { }
    public void LogInformation(string message, params object?[] args) { }
    public void LogWarning(string message, params object?[] args) { }
    public void LogError(Exception? exception, string message, params object?[] args) { }
    public void LogCritical(Exception? exception, string message, params object?[] args) { }
}
