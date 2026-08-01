using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record Identity
{
    public static readonly Identity Anonymous = new("haas", "anonymous");

    public string Issuer { get; }
    public string Subject { get; }
    public ImmutableDictionary<string, ImmutableHashSet<string>> Claims { get; }
    public string Key => $"{Issuer}:{Subject}";

    public Identity(
        string issuer,
        string subject,
        ImmutableDictionary<string, ImmutableHashSet<string>>? claims = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        Issuer = issuer;
        Subject = subject;
        Claims = (claims ?? ImmutableDictionary<string, ImmutableHashSet<string>>.Empty)
            .WithComparers(StringComparer.Ordinal);
    }

    public bool HasClaimType(string claimType) => Claims.ContainsKey(claimType);

    public bool HasClaim(string claimType, string value)
        => Claims.TryGetValue(claimType, out var values) && values.Contains(value);

    public IReadOnlySet<string> GetClaimValues(string claimType)
        => Claims.TryGetValue(claimType, out var values)
            ? values
            : ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

    public bool Equals(Identity? other)
        => ReferenceEquals(this, other)
            || other is not null
            && StringComparer.Ordinal.Equals(Issuer, other.Issuer)
            && StringComparer.Ordinal.Equals(Subject, other.Subject);

    public override int GetHashCode()
        => HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(Issuer),
            StringComparer.Ordinal.GetHashCode(Subject));
}
