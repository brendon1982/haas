using HaaS.Domain.Ports;
using HaaS.Adapters.Agent;
using HaaS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Host.CLI;

public class ChatModule : ICliModule
{
    public string Name => "AI Chat";
    public string Description => "Interactive AI chat session";

    public async Task RunAsync(CancellationToken ct = default)
    {
        var modelId = Environment.GetEnvironmentVariable("HAAS_MODEL") ?? "gemma4";
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "ollama";

        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHaas()
                    .WithSqlitePersistence("data", includeConfig: false)
                    .WithInMemoryConfig(config =>
                    {
                        config.UseOllama();
                        config.UseOpenRouter();
                    })
                    .AddSignalSource<ChatSignalSource, CliSignalPresenter>(config =>
                    {
                        config.UseProvider(providerName)
                            .UseModel(modelId)
                            .UseSystemPrompt("You are an assistant taking part in a long running asynchronous conversation. Reply naturally and concisely. After each reply, the system delivers it to the user and waits for their next message.")
                            .AddTool("get_time");
                    });
            })
            .Build();

        var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
            "Gets the current UTC time for a given timezone");

        Console.Out.WriteLine($"HaaS CLI Chat — model: {modelId}");
        Console.Out.WriteLine("Press Ctrl+C to exit. Empty line to quit.");
        Console.Out.Write("> ");
        Console.Out.Flush();

        await host.RunAsync(ct);
    }
}
