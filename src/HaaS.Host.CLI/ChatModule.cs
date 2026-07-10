using HaaS.Adapters.Agent;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Host.CLI;

public class ChatModule : ICliModule
{
    private readonly IServiceProvider _provider;
    private readonly ChatSignalSource _signalSource;

    public ChatModule()
    {
        _signalSource = new ChatSignalSource();
        var services = new ServiceCollection();
        services.AddHaas()
            .WithInMemoryConfig(config =>
            {
                config.UseOllama();
                config.UseOpenRouter();
            });
        _provider = services.BuildServiceProvider();
    }

    public string Name => "AI Chat";
    public string Description => "Interactive AI chat session";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var modelId = "gemma4:12b";
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";

        var toolRegistry = _provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
            "Gets the current UTC time for a given timezone");

        var signalSourceConfigRepo = _provider.GetRequiredService<ISignalSourceConfigRepository>();
        await signalSourceConfigRepo.SaveAsync(new SignalSourceConfig(
            SourceType: "chat",
            Provider: providerName,
            ModelId: modelId,
            SystemPrompt: "You are an assistant taking part in a long running asynchronous conversation. Reply naturally and concisely. After each reply, the system delivers it to the user and waits for their next message.",
            ToolBelt: new ToolBelt(["get_time"]),
            ThinkingLevel: "on"
        ));

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

            var useCase = _provider.GetRequiredService<RunSessionUseCase>();
            var presenter = new CliSignalPresenter();

            await _signalSource.ListenAsync(async signal =>
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
