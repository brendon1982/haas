using System.ClientModel;
using HaaS.Adapters.Agent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace HaaS.Host.CLI;

public class HaasCliOptions
{
    private readonly List<Action<ChatClientFactory, IProviderConfigRepository>> _registrations = [];

    internal IReadOnlyList<Action<ChatClientFactory, IProviderConfigRepository>> Registrations => _registrations;

    public void UseOllama(string endpoint = "http://localhost:11434")
    {
        _registrations.Add((factory, configRepo) =>
        {
            configRepo.SaveAsync(new ProviderConfig("ollama", endpoint)).GetAwaiter().GetResult();
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
        _registrations.Add((factory, configRepo) =>
        {
            var resolvedEndpoint = endpoint ?? Environment.GetEnvironmentVariable("HAAS_OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1";
            var resolvedApiKey = apiKey ?? Environment.GetEnvironmentVariable("HAAS_OPENROUTER_API_KEY");
            configRepo.SaveAsync(new ProviderConfig("openrouter", resolvedEndpoint, resolvedApiKey)).GetAwaiter().GetResult();
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
