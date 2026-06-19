namespace HaaS.Domain.ValueObjects;

public sealed record AgentIterationEvent(
    string SessionId,
    int Iteration,
    string Phase,
    string? Input,
    string? Output,
    DateTime Timestamp);
