using System.Text.Json;
using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class SessionRecordTestBuilder
{
    private string _sessionId = "sess-default";
    private string _sourceType = "test";
    private string _status = "created";
    private string _provider = "ollama";
    private string _modelId = "gemma4";
    private string _systemPrompt = "You are a helpful assistant.";
    private ToolBelt _toolBelt = ToolBelt.Empty;
    private string _thinkingLevel = "off";
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    private SessionRecordTestBuilder() { }

    public static SessionRecordTestBuilder Create() => new();

    public SessionRecordTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public SessionRecordTestBuilder WithSourceType(string sourceType)
    {
        _sourceType = sourceType;
        return this;
    }

    public SessionRecordTestBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public SessionRecordTestBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public SessionRecordTestBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public SessionRecordTestBuilder WithSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
        return this;
    }

    public SessionRecordTestBuilder WithToolBelt(ToolBelt toolBelt)
    {
        _toolBelt = toolBelt;
        return this;
    }

    public SessionRecordTestBuilder WithThinkingLevel(string level)
    {
        _thinkingLevel = level;
        return this;
    }

    public SessionRecordTestBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public SessionRecordTestBuilder WithUpdatedAt(DateTimeOffset updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public SessionRecord Build() => new(
        _sessionId, _sourceType, _status,
        _provider, _modelId, _systemPrompt,
        JsonSerializer.Serialize(_toolBelt), _thinkingLevel,
        _createdAt, _updatedAt);
}
