using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

var modelId = args.Length > 0 ? args[0] : "gemma4";
var systemPrompt = args.Length > 1
    ? string.Join(" ", args[1..])
    : "You are a helpful assistant. Be concise and accurate.";

var services = new ServiceCollection();
services.AddHaasCore();
var provider = services.BuildServiceProvider();

var configRepo = provider.GetRequiredService<IProviderConfigRepository>();
await configRepo.SaveAsync(new ProviderConfig("ollama", "http://localhost:11434"));

var signalSourceConfigRepo = provider.GetRequiredService<ISignalSourceConfigRepository>();
await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
    SourceType: "cli",
    Provider: "ollama",
    ModelId: modelId,
    SystemPrompt: systemPrompt,
    ToolBelt: ToolBelt.Empty,
    ThinkingLevel: "off"
));

var clientFactory = provider.GetRequiredService<ChatClientFactory>();
clientFactory.Register("ollama", (providerConfig, mdlId) =>
    new OllamaChatClient(new Uri(providerConfig.Endpoint), mdlId));

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.Out.WriteLine("\nShutting down...");
    Environment.Exit(0);
};

Console.Out.WriteLine($"HaaS CLI Chat — model: {modelId}");
Console.Out.WriteLine("Press Ctrl+C to exit. Empty line to quit.");
Console.Out.Write("> ");
Console.Out.Flush();

var useCase = provider.GetRequiredService<RunSessionUseCase>();
var signalSource = provider.GetRequiredService<ISignalSource>();

string? sessionId = null;
await signalSource.ListenAsync(async signal =>
{
    var signalWithSession = signal with { SessionId = sessionId };
    sessionId = await useCase.ExecuteAsync(signalWithSession);
});
