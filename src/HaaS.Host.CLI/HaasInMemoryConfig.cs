using HaaS.Domain.ValueObjects;

namespace HaaS.Host.CLI;

public class HaasInMemoryConfig
{
    private readonly List<ProviderConfig> _providerConfigs = [];

    internal IReadOnlyList<ProviderConfig> ProviderConfigs => _providerConfigs;
    internal bool HasOllama { get; private set; }
    internal bool HasOpenRouter { get; private set; }

    public void UseOllama(string endpoint = "http://localhost:11434")
    {
        _providerConfigs.Add(new ProviderConfig("ollama", endpoint));
        HasOllama = true;
    }

    public void UseOpenRouter(string? endpoint = null, string? apiKey = null)
    {
        var resolvedEndpoint = endpoint ?? Environment.GetEnvironmentVariable("HAAS_OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1";
        var resolvedApiKey = apiKey ?? Environment.GetEnvironmentVariable("HAAS_OPENROUTER_API_KEY");
        _providerConfigs.Add(new ProviderConfig("openrouter", resolvedEndpoint, resolvedApiKey));
        HasOpenRouter = true;
    }
}
