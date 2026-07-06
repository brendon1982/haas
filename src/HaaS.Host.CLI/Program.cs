using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Microsoft.Extensions.DependencyInjection;

var modelId = args.Length > 0 ? args[0] : "gemma4";
var systemPrompt = args.Length > 1
    ? string.Join(" ", args[1..])
    : "You are an assistant taking part in a long running asynchronous conversation. You can only reply via the `reply_to_user` tool. Once you have replied using the `reply_to_user` tool say `waiting for user reply` so that the systems knows you are ready for a user message. You will have to repeat this pattern throughout the conversation. You can reply multiple times before receiving a user reply, however, DO NOT spam the user, only reply as many times as is needed.";

var services = new ServiceCollection();
services.AddHaasCore();
var provider = services.BuildServiceProvider();

var configRepo = provider.GetRequiredService<IProviderConfigRepository>();
await configRepo.SaveAsync(new ProviderConfig("ollama", "http://localhost:11434"));

var toolRegistry = provider.GetRequiredService<IToolRegistry>();
toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
    "Gets the current UTC time for a given timezone");

toolRegistry.Register("reply_to_user", (Func<string, Task<string>>)(async message =>
{
    await Console.Out.WriteLineAsync(message);
    return "Your message has been delivered to the user.";
}), "Sends a message to the user.");

var signalSourceConfigRepo = provider.GetRequiredService<ISignalSourceConfigRepository>();
await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
    SourceType: "cli",
    Provider: "ollama",
    ModelId: modelId,
    SystemPrompt: systemPrompt,
    ToolBelt: new ToolBelt(["get_time", "reply_to_user"]),
    ThinkingLevel: "off"
    // ReplyTool: "reply_to_user"
));

var clientFactory = provider.GetRequiredService<ChatClientFactory>();
clientFactory.Register("ollama",
    (providerConfig, mdlId) => new OllamaApiClient(new Uri(providerConfig.Endpoint), mdlId),
    (options, config) =>
    {
        if (config.ThinkingLevel is not null and not "off")
            options.AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = true };
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

var useCase = provider.GetRequiredService<RunSessionUseCase>();
var signalSource = provider.GetRequiredService<ISignalSource>();

string? sessionId = null;
await signalSource.ListenAsync(async signal =>
{
    var signalWithSession = signal with { SessionId = sessionId };
    sessionId = await useCase.ExecuteAsync(signalWithSession);
});

