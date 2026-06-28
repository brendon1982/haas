using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class AgentSessionConfigTestBuilder
{
    private string _provider = "ollama";
    private string _modelId = "gemma4";
    private string _systemPrompt = "You are a helpful assistant.";
    private IReadOnlyList<string> _tools = [];
    private string _thinkingLevel = "off";
    private string? _endpoint = null;

    private AgentSessionConfigTestBuilder() { }

    public static AgentSessionConfigTestBuilder Create() => new();

    public AgentSessionConfigTestBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public AgentSessionConfigTestBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public AgentSessionConfigTestBuilder WithSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
        return this;
    }

    public AgentSessionConfigTestBuilder WithTools(IReadOnlyList<string> tools)
    {
        _tools = tools;
        return this;
    }

    public AgentSessionConfigTestBuilder WithThinkingLevel(string level)
    {
        _thinkingLevel = level;
        return this;
    }

    public AgentSessionConfigTestBuilder WithEndpoint(string? endpoint)
    {
        _endpoint = endpoint;
        return this;
    }

    public AgentSessionConfig Build() => new(_provider, _modelId, _systemPrompt, _tools, _thinkingLevel, _endpoint);
}
