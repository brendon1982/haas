using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class SignalSourceConfigTestBuilder
{
    private string _sourceType = "default_source";
    private string _provider = "default_provider";
    private string _modelId = "default_model";
    private string _systemPrompt = "You are a default assistant.";
    private ToolBelt _toolBelt = ToolBelt.Empty;
    private string _observabilityMode = "off";

    private SignalSourceConfigTestBuilder() { }

    public static SignalSourceConfigTestBuilder Create() => new();

    public SignalSourceConfigTestBuilder WithSourceType(string sourceType)
    {
        _sourceType = sourceType;
        return this;
    }

    public SignalSourceConfigTestBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public SignalSourceConfigTestBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public SignalSourceConfigTestBuilder WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    public SignalSourceConfigTestBuilder WithToolBelt(ToolBelt toolBelt)
    {
        _toolBelt = toolBelt;
        return this;
    }

    public SignalSourceConfigTestBuilder WithObservabilityMode(string mode)
    {
        _observabilityMode = mode;
        return this;
    }

    public SignalSourceConfig Build() => new(_sourceType, _provider, _modelId, _systemPrompt, _toolBelt, _observabilityMode);
}
