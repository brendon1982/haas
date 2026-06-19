using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class SessionConfigTestBuilder
{
    private string _provider = "ollama";
    private string _modelId = "gemma4";
    private string _systemPrompt = "You are a helpful assistant.";
    private IReadOnlyList<string> _tools = [];
    private string _thinkingLevel = "off";

    private SessionConfigTestBuilder() { }

    public static SessionConfigTestBuilder Create() => new();

    public SessionConfigTestBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public SessionConfigTestBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public SessionConfigTestBuilder WithSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
        return this;
    }

    public SessionConfigTestBuilder WithTools(IReadOnlyList<string> tools)
    {
        _tools = tools;
        return this;
    }

    public SessionConfigTestBuilder WithThinkingLevel(string level)
    {
        _thinkingLevel = level;
        return this;
    }

    public AgentSessionConfig Build() => new(_provider, _modelId, _systemPrompt, _tools, _thinkingLevel);
}
