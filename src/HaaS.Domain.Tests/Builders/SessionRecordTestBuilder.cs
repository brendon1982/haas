using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public class SessionRecordTestBuilder
{
    private string _sessionId = "sess-default";
    private string _sourceType = "test";
    private string _status = "created";
    private byte[]? _agentState;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

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

    public SessionRecordTestBuilder WithAgentState(byte[]? agentState)
    {
        _agentState = agentState;
        return this;
    }

    public SessionRecordTestBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public SessionRecordTestBuilder WithUpdatedAt(DateTime updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public SessionRecord Build() => new(_sessionId, _sourceType, _status, _agentState, _createdAt, _updatedAt);
}
