using HaaS.Domain.ValueObjects;

namespace HaaS.Infrastructure;

public class SignalSourceConfigBuilder
{
    private readonly string _sourceType;
    private string _provider = "openai";
    private string _modelId = "gpt-4o";
    private string _systemPrompt = "You are a helpful assistant.";
    private string _thinkingLevel = "off";
    private readonly List<string> _toolBelt = new();

    public SignalSourceConfigBuilder(string sourceType)
    {
        _sourceType = sourceType;
    }

    public SignalSourceConfigBuilder UseProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public SignalSourceConfigBuilder UseModel(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public SignalSourceConfigBuilder UseSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    public SignalSourceConfigBuilder UseThinkingLevel(string level)
    {
        _thinkingLevel = level;
        return this;
    }

    public SignalSourceConfigBuilder AddTool(string toolName)
    {
        _toolBelt.Add(toolName);
        return this;
    }

    public SignalSourceConfig Build()
    {
        return new SignalSourceConfig(
            _sourceType,
            _provider,
            _modelId,
            _systemPrompt,
            new ToolBelt(_toolBelt),
            _thinkingLevel);
    }
}
