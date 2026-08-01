using HaaS.Adapters.Agent;
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
public class SignalContextScopeTests
{
    [Test]
    public void AddHaas_RegistersAccessorAndScopeAsTheSameScopedInstance()
    {
        // Arrange
        using var provider = SutBuilder.Create().Build();
        using var scope = provider.CreateScope();

        // Act
        var accessor = scope.ServiceProvider.GetRequiredService<ISignalContextAccessor>();
        var contextScope = scope.ServiceProvider.GetRequiredService<ISignalContextScope>();

        // Assert
        Expect(ReferenceEquals(accessor, contextScope)).To.Be.True();
        Expect(() => _ = accessor.Current)
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("No signal execution context is active");
    }

    [Test]
    public void Push_WhenContextAlreadyActive_RejectsAndDisposalClearsCurrentContext()
    {
        // Arrange
        using var provider = SutBuilder.Create().Build();
        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISignalContextAccessor>();
        var contextScope = scope.ServiceProvider.GetRequiredService<ISignalContextScope>();
        var expected = SignalExecutionContextTestBuilder.Create().Build();

        // Act
        using (contextScope.Push(expected))
        {
            // Assert
            Expect(accessor.Current).To.Equal(expected);
            Expect(() => contextScope.Push(expected))
                .To.Throw<InvalidOperationException>()
                .With.Message.Containing("already active");
        }

        // Assert
        Expect(() => _ = accessor.Current)
            .To.Throw<InvalidOperationException>();
    }

    [Test]
    public async Task Push_InConcurrentScopes_IsolatesEachExecutionContext()
    {
        // Arrange
        using var provider = SutBuilder.Create().Build();
        var first = SignalExecutionContextTestBuilder.Create()
            .WithSessionId("session-first")
            .WithSource("source-first")
            .Build();
        var second = SignalExecutionContextTestBuilder.Create()
            .WithSessionId("session-second")
            .WithSource("source-second")
            .Build();
        var bothActive = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var activeCount = 0;

        async Task<SignalExecutionContext> ReadInScopeAsync(SignalExecutionContext expected)
        {
            using var scope = provider.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<ISignalContextAccessor>();
            var contextScope = scope.ServiceProvider.GetRequiredService<ISignalContextScope>();
            using var pushed = contextScope.Push(expected);
            if (Interlocked.Increment(ref activeCount) == 2)
            {
                bothActive.SetResult();
            }

            await release.Task;
            return accessor.Current;
        }

        // Act
        var firstRead = ReadInScopeAsync(first);
        var secondRead = ReadInScopeAsync(second);
        await bothActive.Task;
        release.SetResult();
        var contexts = await Task.WhenAll(firstRead, secondRead);

        // Assert
        Expect(contexts).To.Equal([first, second]);
    }

    [Test]
    public async Task RegisteredGenericTool_ReceivesCurrentSessionSourceAndAuthentication()
    {
        // Arrange
        var services = SutBuilder.Create().BuildServices();
        services.AddScoped<ContextReadingTool>();
        using var provider = services.BuildServiceProvider();
        var toolProvider = provider.GetRequiredService<IToolProvider>();
        toolProvider.Register<ContextReadingTool>(
            "context",
            "Reads the current execution context",
            tool => (Func<Task<string>>)tool.ReadAsync);
        var context = SignalExecutionContextTestBuilder.Create()
            .WithSessionId("session-42")
            .WithSource("webhook")
            .WithAuthentication(AuthenticationContextTestBuilder.Create()
                .WithIdentity(IdentityTestBuilder.Create()
                    .WithIssuer("issuer")
                    .WithSubject("subject")
                    .WithClaim("role", "operator")
                    .Build())
                .WithAuthenticationMethod("oauth")
                .Build())
            .Build();
        using var signalScope = provider.CreateScope();
        provider.GetRequiredService<ISignalScopeAccessor>().ServiceProvider = signalScope.ServiceProvider;
        var contextScope = signalScope.ServiceProvider.GetRequiredService<ISignalContextScope>();

        // Act
        using (contextScope.Push(context))
        {
            var tool = toolProvider.GetTools(["context"]).Single();
            var result = await (Task<string>)tool.Handler.DynamicInvoke()!;

            // Assert
            Expect(result).To.Equal($"{context.SessionId}:{context.Source}:{context.Authentication.Identity.Issuer}:{context.Authentication.Identity.Subject}:{context.Authentication.AuthenticationMethod}");
        }

        provider.GetRequiredService<ISignalScopeAccessor>().ServiceProvider = null;
    }
}

file sealed class ContextReadingTool(ISignalContextAccessor contextAccessor)
{
    public Task<string> ReadAsync()
    {
        var context = contextAccessor.Current;
        return Task.FromResult(
            $"{context.SessionId}:{context.Source}:{context.Authentication.Identity.Issuer}:{context.Authentication.Identity.Subject}:{context.Authentication.AuthenticationMethod}");
    }
}

file sealed class SutBuilder
{
    public static SutBuilder Create() => new();

    public ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddHaas();
        return services;
    }

    public ServiceProvider Build() => BuildServices().BuildServiceProvider();
}
