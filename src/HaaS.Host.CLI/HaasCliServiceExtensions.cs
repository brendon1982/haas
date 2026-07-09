using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OpenAI;
using System.ClientModel;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static IServiceCollection WithInMemoryConfig(
        this HaasBuilder builder,
        Action<HaasInMemoryConfig>? configure = null)
    {
        var services = builder.Services;
        var config = new HaasInMemoryConfig();
        configure?.Invoke(config);
        services.AddSingleton(config);
        services.AddSingleton<IProviderConfigRepository>(
            new InMemoryProviderConfigRepository(config.ProviderConfigs));
        return services;
    }

    public static IServiceCollection AddSignalSources(this IServiceCollection services)
    {
        services.AddTransient<ISignalSource, CliSignalSource>();
        return services;
    }

    public static IServiceProvider SetupProviderFactories(this IServiceProvider services)
    {
        var config = services.GetRequiredService<HaasInMemoryConfig>();
        var factory = services.GetRequiredService<ChatClientFactory>();

        if (config.HasOllama)
        {
            factory.Register("ollama",
                (cfg, modelId) => new OllamaApiClient(new Uri(cfg.Endpoint), modelId),
                (options, cfg) =>
                {
                    if (cfg.ThinkingLevel is not null and not "off")
                        options.AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = true };
                });
        }

        if (config.HasOpenRouter)
        {
            factory.Register("openrouter",
                (cfg, modelId) =>
                {
                    var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(cfg.Endpoint) };
                    var credential = new ApiKeyCredential(cfg.ApiKey!);
                    var chatClient = new OpenAI.Chat.ChatClient(modelId, credential, openAiOptions);
                    return chatClient.AsIChatClient();
                });
        }

        return services;
    }
}
