namespace HaaS.Domain.ValueObjects;

public record QueuedSignal(
    string Id,
    Signal Signal,
    Identity Identity,
    SignalStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PickedAt = null,
    DateTimeOffset? CompletedAt = null,
    int RetryCount = 0,
    int MaxRetries = 3
);

public enum SignalStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
