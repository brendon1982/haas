using System.Runtime.CompilerServices;
using System.Text.Json;
using HaaS.Adapters.Agent;
using HaaS.Application;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NExpect;
using NUnit.Framework;
using static NExpect.Expectations;

namespace HaaS.Infrastructure.Tests;

[TestFixture]
public class LlmIsolationBoundaryIntegrationTests
{
    [Test]
    public async Task DirectProcessing_KeepsSignalContextOutOfLlmInputsWhileMakingItAvailableToTheTool()
    {
        // Arrange
        var markers = IsolationMarkers.Create();
        using var sut = SutBuilder.Create().Build();
        var envelope = CreateEnvelope("direct-session", markers);

        // Act
        await sut.ExecuteDirectAsync(envelope);

        // Assert
        Expect(sut.ChatClient.CapturedMessagePayloads.Count).Not.To.Equal(0);
        Expect(sut.ChatClient.CapturedToolSchemas.Count).Not.To.Equal(0);
        AssertNoMarkers(sut.ChatClient.CapturedMessagePayloads, markers);
        AssertNoMarkers(sut.ChatClient.CapturedToolSchemas, markers);
        await AssertPersistedMessagesContainNoMarkersAsync(sut, "direct-session", markers);
        AssertToolObservedContext(sut.ObservedContext, markers);
    }

    [Test]
    public async Task QueuedProcessing_KeepsSignalContextOutOfLlmInputsWhileMakingItAvailableToTheTool()
    {
        // Arrange
        var markers = IsolationMarkers.Create();
        using var sut = SutBuilder.Create().Build();
        var envelope = CreateEnvelope("queued-session", markers);

        // Act
        await sut.ExecuteQueuedAsync(envelope);

        // Assert
        Expect(sut.ChatClient.CapturedMessagePayloads.Count).Not.To.Equal(0);
        Expect(sut.ChatClient.CapturedToolSchemas.Count).Not.To.Equal(0);
        AssertNoMarkers(sut.ChatClient.CapturedMessagePayloads, markers);
        AssertNoMarkers(sut.ChatClient.CapturedToolSchemas, markers);
        await AssertPersistedMessagesContainNoMarkersAsync(sut, "queued-session", markers);
        AssertToolObservedContext(sut.ObservedContext, markers);
    }

    private static SignalEnvelope CreateEnvelope(string sessionId, IsolationMarkers markers)
    {
        var context = SignalContextTestBuilder.Create()
            .WithAuthentication(AuthenticationContextTestBuilder.Create()
                .WithIdentity(IdentityTestBuilder.Create()
                    .WithIssuer(markers.Issuer)
                    .WithSubject(markers.Subject)
                    .WithClaim(markers.ClaimType, markers.ClaimValue)
                    .Build())
                .WithCredentialReference(markers.CredentialName, markers.CredentialProvider, markers.CredentialReference)
                .Build())
            .WithAttribute(markers.AttributeName, markers.AttributeValue)
            .Build();

        return SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create()
                .WithSource(IsolationHarness.SourceType)
                .WithSessionId(sessionId)
                .WithPayload("invoke the isolation tool")
                .Build())
            .WithContext(context)
            .Build();
    }

    private static async Task AssertPersistedMessagesContainNoMarkersAsync(
        IsolationHarness sut,
        string sessionId,
        IsolationMarkers markers)
    {
        var persistedMessages = await sut.MessageStore.GetMessagesAsync(sessionId);
        Expect(persistedMessages.Count).Not.To.Equal(0);

        var persistedContentAndPayload = persistedMessages.Select(message =>
            $"{message.Content}\n{message.Payload}");
        AssertNoMarkers(persistedContentAndPayload, markers);
    }

    private static void AssertNoMarkers(IEnumerable<string> values, IsolationMarkers markers)
    {
        var allValues = string.Join("\n", values);
        foreach (var marker in markers.All)
        {
            Expect(allValues).Not.To.Contain(marker);
        }
    }

    private static void AssertToolObservedContext(
        SignalExecutionContext? observedContext,
        IsolationMarkers markers)
    {
        Expect(observedContext).Not.To.Be.Null();
        Expect(observedContext!.Authentication.Identity.Issuer).To.Equal(markers.Issuer);
        Expect(observedContext.Authentication.Identity.Subject).To.Equal(markers.Subject);
        Expect(observedContext.Authentication.Identity.GetClaimValues(markers.ClaimType)
            .Contains(markers.ClaimValue)).To.Be.True();
        Expect(observedContext.Attributes[markers.AttributeName]).To.Equal(markers.AttributeValue);

        var credential = observedContext.Authentication.CredentialReferences[markers.CredentialName];
        Expect(credential.Provider).To.Equal(markers.CredentialProvider);
        Expect(credential.Reference).To.Equal(markers.CredentialReference);
    }
}

internal sealed record IsolationMarkers(
    string Issuer,
    string Subject,
    string ClaimType,
    string ClaimValue,
    string AttributeName,
    string AttributeValue,
    string CredentialName,
    string CredentialProvider,
    string CredentialReference)
{
    public IEnumerable<string> All =>
    [
        Issuer,
        Subject,
        ClaimType,
        ClaimValue,
        AttributeName,
        AttributeValue,
        CredentialName,
        CredentialProvider,
        CredentialReference
    ];

    public static IsolationMarkers Create() => new(
        "issuer-marker-7f4e",
        "subject-marker-9a1c",
        "claim-type-marker-42d8",
        "claim-value-marker-6b03",
        "attribute-name-marker-e718",
        "attribute-value-marker-c529",
        "credential-name-marker-a64f",
        "credential-provider-marker-d237",
        "credential-reference-marker-b851");
}

internal sealed class IsolationContextObservation
{
    public SignalExecutionContext? Context { get; set; }
}

file sealed class IsolationContextTool(
    ISignalContextAccessor signalContextAccessor,
    IsolationContextObservation observation)
{
    public Task<string> ExecuteAsync(string input)
    {
        observation.Context = signalContextAccessor.Current;
        return Task.FromResult(input);
    }
}

internal sealed class CapturingToolChatClient(string toolName) : IChatClient
{
    private bool _hasRequestedTool;

    public List<string> CapturedMessagePayloads { get; } = [];
    public List<string> CapturedToolSchemas { get; } = [];
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CapturedMessagePayloads.AddRange(messages.Select(message => JsonSerializer.Serialize(message)));
        CapturedToolSchemas.AddRange(options?.Tools?
            .OfType<AIFunction>()
            .Select(tool => tool.JsonSchema.GetRawText())
            ?? []);

        if (!_hasRequestedTool)
        {
            _hasRequestedTool = true;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "isolation-call",
                    toolName,
                    new Dictionary<string, object?> { ["input"] = "model-authored-input" })
            ])));
        }

        Completion.TrySetResult();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

file sealed class CapturingChatClientFactory(CapturingToolChatClient chatClient) : IChatClientFactory
{
    public bool CanCreate(string provider) => true;

    public Task<IChatClient> CreateAsync(string provider, string modelId)
        => Task.FromResult<IChatClient>(chatClient);

    public void ConfigureOptions(string provider, ChatOptions options, AgentSessionConfig config) { }
}

file sealed class IsolationPresenter : ISignalPresenter
{
    public Task PresentProcessingAsync(string sessionId, string? messageId = null) => Task.CompletedTask;
    public Task PresentAsync(SessionResult result) => Task.CompletedTask;
    public Task PresentErrorAsync(string? sessionId, Exception exception) => Task.CompletedTask;
}

file sealed class IsolationSignalSource : ISignalSource
{
    public string Type => IsolationHarness.SourceType;
    public Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler) => Task.CompletedTask;
    public Task ShutdownAsync() => Task.CompletedTask;
}

internal sealed class IsolationHarness : IDisposable
{
    public const string SourceType = "isolation-source";
    public const string ToolName = "isolation-tool";

    private readonly ServiceProvider _provider;
    private readonly SignalSourceRegistration _registration;

    public IsolationHarness(
        ServiceProvider provider,
        SignalSourceRegistration registration,
        CapturingToolChatClient chatClient,
        IMessageStore messageStore,
        IsolationContextObservation observation)
    {
        _provider = provider;
        _registration = registration;
        ChatClient = chatClient;
        MessageStore = messageStore;
        Observation = observation;
    }

    private IsolationContextObservation Observation { get; }
    public CapturingToolChatClient ChatClient { get; }
    public IMessageStore MessageStore { get; }
    public SignalExecutionContext? ObservedContext => Observation.Context;

    public async Task ExecuteDirectAsync(SignalEnvelope envelope)
    {
        var engine = _provider.GetRequiredService<DirectHaasEngine>();
        await engine.ProcessSignalAsync(envelope, _registration);
    }

    public async Task ExecuteQueuedAsync(SignalEnvelope envelope)
    {
        var engine = _provider.GetRequiredService<QueuedHaasEngine>();
        using var cancellation = new CancellationTokenSource();
        await engine.StartAsync(cancellation.Token);
        try
        {
            await engine.ProcessSignalAsync(envelope, _registration);
            await ChatClient.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            cancellation.Cancel();
            await engine.StopAsync(CancellationToken.None);
        }
    }

    public void Dispose() => _provider.Dispose();
}

file sealed class SutBuilder
{
    private SutBuilder() { }

    public static SutBuilder Create() => new();

    public IsolationHarness Build()
    {
        var services = new ServiceCollection();
        services.AddHaas();

        var chatClient = new CapturingToolChatClient(IsolationHarness.ToolName);
        services.RemoveAll<IChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(new CapturingChatClientFactory(chatClient));
        services.AddSingleton<IsolationContextObservation>();
        services.AddScoped<IsolationContextTool>();

        var provider = services.BuildServiceProvider();
        var config = SignalSourceConfigTestBuilder.Create()
            .WithSourceType(IsolationHarness.SourceType)
            .WithProvider("isolation-provider")
            .WithToolBelt(new ToolBelt([IsolationHarness.ToolName]))
            .Build();
        var registration = new SignalSourceRegistration(
            new IsolationSignalSource(),
            new IsolationPresenter(),
            config);
        provider.GetRequiredService<ISignalSourceRegistry>().Register(registration);
        provider.GetRequiredService<IToolProvider>().Register<IsolationContextTool>(
            IsolationHarness.ToolName,
            "Invokes the isolation boundary test tool.",
            tool => (Func<string, Task<string>>)tool.ExecuteAsync);

        return new IsolationHarness(
            provider,
            registration,
            chatClient,
            provider.GetRequiredService<IMessageStore>(),
            provider.GetRequiredService<IsolationContextObservation>());
    }
}
