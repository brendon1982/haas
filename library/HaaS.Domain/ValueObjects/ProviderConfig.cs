namespace HaaS.Domain.ValueObjects;

public record ProviderConfig(
    string Provider,
    string Endpoint,
    string? ApiKey = null
);
