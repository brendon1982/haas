using HaaS.Domain.Ports;
using HaaS.Adapters.Agent;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HaaS.Host.CLI;

public class ChatModule : ICliModule
{
    public string Name => "AI Chat";
    public string Description => "Interactive AI chat session";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var modelId = "gemma2";
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";

        var services = new ServiceCollection();
        services.AddHaas()
            .AddSignalSource<ChatSignalSource, CliSignalPresenter>(config =>
            {
                config.UseProvider(providerName)
                    .UseModel(modelId)
                    .UseSystemPrompt("You are an assistant taking part in a long running asynchronous conversation. Reply naturally and concisely. After each reply, the system delivers it to the user and waits for their next message.")
                    .AddTool("get_time");
            });

        var provider = services.BuildServiceProvider();

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
            "Gets the current UTC time for a given timezone");

        var engine = provider.GetRequiredService<IHaasEngine>();

        Console.Out.WriteLine($"HaaS CLI Chat — model: {modelId}");
        Console.Out.WriteLine("Press Ctrl+C to exit. Empty line to quit.");
        Console.Out.Write("> ");
        Console.Out.Flush();

        await engine.RunAsync(ct);
    }
}
