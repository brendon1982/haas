using HaaS.Adapters.Agent;
using HaaS.Adapters.Execution;
using HaaS.Adapters.Observability;
using HaaS.Adapters.Persistence;
using HaaS.Adapters.Signal;
using HaaS.Adapters.Store;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

var modelId = args.Length > 0 ? args[0] : "gemma4";
var systemPrompt = args.Length > 1
    ? string.Join(" ", args[1..])
    : "You are a helpful assistant. Be concise and accurate.";

var config = new AgentSessionConfig(
    Provider: "ollama",
    ModelId: modelId,
    SystemPrompt: systemPrompt,
    Tools: [],
    ThinkingLevel: "off"
);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.Out.WriteLine("\nShutting down...");
    Environment.Exit(0);
};

IChatClientFactory chatClientFactory = new ChatClientFactory(cfg =>
    new OllamaChatClient(
        new Uri("http://localhost:11434"),
        cfg.ModelId));

ISessionRepository sessionRepo = new InMemorySessionRepository();
IMessageStore messageStore = new InMemorySessionMessageStore();
ILogger logger = new ConsoleLogger();
IAgentStrategy innerStrategy = new MicrosoftAgentFrameworkStrategy(chatClientFactory, sessionRepo, messageStore);
IAgentStrategy strategy = new ObservableAgentStrategy(innerStrategy, logger);
IExecutionTarget target = new ConsoleExecutionTarget();
var useCase = new RunSessionUseCase(strategy, target, sessionRepo, TimeProvider.System);

Console.Out.WriteLine($"HaaS CLI Chat — model: {modelId}");
Console.Out.WriteLine("Press Ctrl+C to exit. Empty line to quit.");
Console.Out.Write("> ");
Console.Out.Flush();

string? sessionId = null;
var source = new CliSignalSource();
await source.ListenAsync(async signal =>
{
    var signalWithSession = signal with { SessionId = sessionId };
    sessionId = await useCase.ExecuteAsync(config, signalWithSession);
});
