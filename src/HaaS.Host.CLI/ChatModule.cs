using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Infrastructure;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Host.CLI;

public class ChatModule : ICliModule
{
    public string Name => "AI Chat";
    public string Description => "Interactive AI chat session";

    public async Task RunAsync(CliLayoutManager layout, CancellationToken ct = default)
    {
        var modelId = Environment.GetEnvironmentVariable("HAAS_MODEL") ?? "cohere/north-mini-code:free";
        var providerName = Environment.GetEnvironmentVariable("HAAS_PROVIDER") ?? "openrouter";

        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHaas()
                    .WithSpectreConsole(layout)
                    .WithSqlitePersistence("chat-data", includeConfig: false)
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

        RegisterTool(host.Services.GetRequiredService<IToolProvider>());

        await host.RunAsync(ct);
    }

    private void RegisterTool(IToolProvider toolProvider)
    {
        toolProvider.Register(new ToolDefinition("get_time", "Gets the current UTC time for a given timezone", (Func<string, Task<string>>)(async timezone =>
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")));
    }
}
