using System.Collections.Immutable;
using System.Security.Claims;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.Web.Infrastructure;

public static class ClaimsPrincipalSignalContextMapper
{
    public static SignalContext ToSignalContext(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated is not true)
        {
            return SignalContext.Anonymous;
        }

        var subjectClaim = principal.FindFirst("sub")
            ?? principal.FindFirst(ClaimTypes.NameIdentifier);
        var subject = subjectClaim?.Value ?? principal.Identity.Name;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException(
                "An authenticated principal must provide a 'sub' or name identifier claim.");
        }

        var issuer = principal.FindFirst("iss")?.Value
            ?? subjectClaim?.Issuer
            ?? principal.Identity.AuthenticationType
            ?? "claims-principal";
        var claims = principal.Claims
            .GroupBy(claim => claim.Type, StringComparer.Ordinal)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Select(claim => claim.Value)
                    .ToImmutableHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        return new SignalContext(new AuthenticationContext(
            new Identity(issuer, subject, claims),
            principal.Identity.AuthenticationType ?? "claims-principal"));
    }
}
