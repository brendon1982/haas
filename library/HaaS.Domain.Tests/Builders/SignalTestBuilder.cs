using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class SignalTestBuilder
{
    private string _payload = "default prompt";
    private string _source = "test";
    private string? _sessionId;
    private DateTimeOffset? _arrivedAt;
    private string? _messageId;

    private SignalTestBuilder() { }

    public static SignalTestBuilder Create() => new();

    public SignalTestBuilder WithPayload(string payload)
    {
        _payload = payload;
        return this;
    }

    public SignalTestBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }

    public SignalTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public SignalTestBuilder WithArrivedAt(DateTimeOffset arrivedAt)
    {
        _arrivedAt = arrivedAt;
        return this;
    }

    public SignalTestBuilder WithMessageId(string messageId)
    {
        _messageId = messageId;
        return this;
    }

    public Signal Build() => new(_payload, _source, _sessionId, _arrivedAt, _messageId);
}
