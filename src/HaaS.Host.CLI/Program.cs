using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

var modelId = args.Length > 0 ? args[0] : "gemma4:12b";
var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";
var systemPrompt = args.Length > 1
    ? string.Join(" ", args[1..])
    : "You are an assistant taking part in a long running asynchronous conversation. Reply naturally and concisely. After each reply, the system delivers it to the user and waits for their next message.";

var services = new ServiceCollection();
services.AddHaasCore();
var serviceProvider = services.BuildServiceProvider();

var configRepo = serviceProvider.GetRequiredService<IProviderConfigRepository>();
await configRepo.SaveAsync(new ProviderConfig("ollama", "http://localhost:11434"));
var openRouterApiKey = Environment.GetEnvironmentVariable("HAAS_OPENROUTER_API_KEY");
var openRouterEndpoint = Environment.GetEnvironmentVariable("HAAS_OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1";
await configRepo.SaveAsync(new ProviderConfig("openrouter", openRouterEndpoint, openRouterApiKey));

var toolRegistry = serviceProvider.GetRequiredService<IToolRegistry>();
toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
    "Gets the current UTC time for a given timezone");

var signalSourceConfigRepo = serviceProvider.GetRequiredService<ISignalSourceConfigRepository>();
await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
    SourceType: "cli",
    Provider: providerName,
    ModelId: modelId,
    SystemPrompt: systemPrompt,
    ToolBelt: new ToolBelt(["get_time"]),
    ThinkingLevel: "off"
));

var clientFactory = serviceProvider.GetRequiredService<ChatClientFactory>();
clientFactory.Register("ollama",
    (providerConfig, mdlId) => new OllamaApiClient(new Uri(providerConfig.Endpoint), mdlId),
    (options, config) =>
    {
        if (config.ThinkingLevel is not null and not "off")
            options.AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = true };
    });

clientFactory.Register("openrouter",
    (providerConfig, mdlId) =>
    {
        var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(providerConfig.Endpoint) };
        var credential = new System.ClientModel.ApiKeyCredential(providerConfig.ApiKey!);
        var chatClient = new OpenAI.Chat.ChatClient(mdlId, credential, openAiOptions);
        return chatClient.AsIChatClient();
    });

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

var useCase = serviceProvider.GetRequiredService<RunSessionUseCase>();
var signalSource = serviceProvider.GetRequiredService<ISignalSource>();
var presenter = new CliSignalPresenter();

await signalSource.ListenAsync(async signal =>
{
    var signalWithSession = signal with { SessionId = presenter.LastSessionId };
    await useCase.ExecuteAsync(signalWithSession, presenter);
});

