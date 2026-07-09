using HaaS.Adapters.Agent;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;

namespace HaaS.Host.CLI;

public class HaasInMemoryConfig
{
    private readonly List<ProviderConfig> _providerConfigs = [];
    private readonly List<Action<ChatClientFactory>> _factoryRegistrations = [];

    internal IReadOnlyList<ProviderConfig> ProviderConfigs => _providerConfigs;
    internal IReadOnlyList<Action<ChatClientFactory>> FactoryRegistrations => _factoryRegistrations;

    public void UseOllama(string endpoint = "http://localhost:11434")
    {
        _providerConfigs.Add(new ProviderConfig("ollama", endpoint));
        _factoryRegistrations.Add(factory =>
        {
            factory.Register("ollama",
                (config, modelId) => new OllamaApiClient(new Uri(config.Endpoint), modelId),
                (options, config) =>
                {
                    if (config.ThinkingLevel is not null and not "off")
                        options.AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = true };
                });
        });
    }

    public void UseOpenRouter(string? endpoint = null, string? apiKey = null)
    {
        var resolvedEndpoint = endpoint ?? Environment.GetEnvironmentVariable("HAAS_OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1";
        var resolvedApiKey = apiKey ?? Environment.GetEnvironmentVariable("HAAS_OPENROUTER_API_KEY");
        _providerConfigs.Add(new ProviderConfig("openrouter", resolvedEndpoint, resolvedApiKey));
        _factoryRegistrations.Add(factory =>
        {
            factory.Register("openrouter",
                (config, modelId) =>
                {
                    var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) };
                    var credential = new ApiKeyCredential(config.ApiKey!);
                    var chatClient = new OpenAI.Chat.ChatClient(modelId, credential, openAiOptions);
                    return chatClient.AsIChatClient();
                });
        });
    }
}
