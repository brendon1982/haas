using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class IncomingSignalTestBuilder
{
    private string _payload = "default prompt";
    private SignalContext _context = SignalContext.Anonymous;
    private string? _sessionId;
    private DateTimeOffset? _arrivedAt;
    private string? _messageId;

    private IncomingSignalTestBuilder() { }

    public static IncomingSignalTestBuilder Create() => new();

    public IncomingSignalTestBuilder WithPayload(string payload)
    {
        _payload = payload;
        return this;
    }

    public IncomingSignalTestBuilder WithContext(SignalContext context)
    {
        _context = context;
        return this;
    }

    public IncomingSignalTestBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public IncomingSignalTestBuilder WithArrivedAt(DateTimeOffset arrivedAt)
    {
        _arrivedAt = arrivedAt;
        return this;
    }

    public IncomingSignalTestBuilder WithMessageId(string messageId)
    {
        _messageId = messageId;
        return this;
    }

    public IncomingSignal Build() => new(_payload, _context, _sessionId, _arrivedAt, _messageId);
}
