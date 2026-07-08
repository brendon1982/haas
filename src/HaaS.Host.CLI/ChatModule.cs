using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OpenAI;

namespace HaaS.Host.CLI;

public class ChatModule : ICliModule
{
    public string Name => "AI Chat";
    public string Description => "Interactive AI chat session";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var services = new ServiceCollection();
        services.AddHaasCore();
        var provider = services.BuildServiceProvider();

        var modelId = "gemma4:12b";
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";

        var configRepo = provider.GetRequiredService<IProviderConfigRepository>();
        await configRepo.SaveAsync(new ProviderConfig("ollama", "http://localhost:11434"));
        var openRouterApiKey = Environment.GetEnvironmentVariable("HAAS_OPENROUTER_API_KEY");
        var openRouterEndpoint = Environment.GetEnvironmentVariable("HAAS_OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1";
        await configRepo.SaveAsync(new ProviderConfig("openrouter", openRouterEndpoint, openRouterApiKey));

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
            "Gets the current UTC time for a given timezone");

        var signalSourceConfigRepo = provider.GetRequiredService<ISignalSourceConfigRepository>();
        await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
            SourceType: "cli",
            Provider: providerName,
            ModelId: modelId,
            SystemPrompt: "You are an assistant taking part in a long running asynchronous conversation. Reply naturally and concisely. After each reply, the system delivers it to the user and waits for their next message.",
            ToolBelt: new ToolBelt(["get_time"]),
            ThinkingLevel: "off"
        ));

        var clientFactory = provider.GetRequiredService<ChatClientFactory>();
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

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            Console.Out.WriteLine($"HaaS CLI Chat — model: {modelId}");
            Console.Out.WriteLine("Press Ctrl+C to exit. Empty line to quit.");
            Console.Out.Write("> ");
            Console.Out.Flush();

            var useCase = provider.GetRequiredService<RunSessionUseCase>();
            var signalSource = provider.GetRequiredService<ISignalSource>();
            var presenter = new CliSignalPresenter();

            await signalSource.ListenAsync(async signal =>
            {
                var signalWithSession = signal with { SessionId = presenter.LastSessionId };
                await useCase.ExecuteAsync(signalWithSession, presenter);
            });
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
