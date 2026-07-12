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

        var services = new ServiceCollection();
        services.AddHaas()
            .WithInMemoryConfig(config =>
            {
                config.UseOllama();
                config.UseOpenRouter();
            })
            .WithSqlitePersistence("data")
            .WithWorkerPool(3)
            .AddSignalSource<ChatSignalSource, CliSignalPresenter>(config =>
            {
                config.UseProvider(providerName)
                    .UseModel(modelId)
                    .UseSystemPrompt("You are an assistant taking part in a long running asynchronous conversation. Reply naturally and concisely. After each reply, the system delivers it to the user and waits for their next message.")
                    .AddTool("get_time");
            });

        var provider = services.BuildServiceProvider();

        // Start background workers
        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(ct);
        }

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register("get_time", (Func<string, Task<string>>)(async timezone =>
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
            "Gets the current UTC time for a given timezone");

        var engine = provider.GetRequiredService<IHaasEngine>();

        Console.Out.WriteLine($"HaaS CLI Chat — model: {modelId} (Worker Pool: 3)");
        Console.Out.WriteLine("Press Ctrl+C to exit. Empty line to quit.");
        Console.Out.Write("> ");
        Console.Out.Flush();

        try
        {
            await engine.RunAsync(ct);
        }
        finally
        {
            foreach (var service in hostedServices)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }
}
