namespace HaaS.Domain.ValueObjects;

public sealed record PolicySubject
{
    public string Issuer { get; }
    public string Subject { get; }

    public PolicySubject(string issuer, string subject)
    {
        Issuer = PolicyValidation.RequireNonEmpty(issuer, nameof(issuer));
        Subject = PolicyValidation.RequireNonEmpty(subject, nameof(subject));
    }
}
