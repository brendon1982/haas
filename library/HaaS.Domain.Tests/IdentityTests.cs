using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;
using NExpect;
using static NExpect.Expectations;

namespace HaaS.Domain.Tests;

[TestFixture]
public class IdentityTests
{
    [Test]
    public void Equality_WithDifferentClaimsAndSameIssuerSubject_IsEqual()
    {
        // Arrange
        var issuer = "https://issuer.example";
        var subject = "user-123";
        var first = IdentityTestBuilder.Create()
            .WithIssuer(issuer)
            .WithSubject(subject)
            .WithClaim("role", "reader")
            .Build();
        var second = IdentityTestBuilder.Create()
            .WithIssuer(issuer)
            .WithSubject(subject)
            .WithClaim("role", "administrator")
            .Build();

        // Act
        var areEqual = first == second;

        // Assert
        Expect(areEqual).To.Be.True();
        Expect(first.GetHashCode()).To.Equal(second.GetHashCode());
        Expect(first.Key).To.Equal(second.Key);
    }

    [Test]
    public void Claims_UsesOrdinalMatchingForTypesAndValues()
    {
        // Arrange
        var claimType = "Role";
        var claimValue = "Administrator";
        var identity = IdentityTestBuilder.Create()
            .WithClaim(claimType, claimValue)
            .Build();

        // Act
        var matchingClaim = identity.HasClaim(claimType, claimValue);
        var mismatchedType = identity.HasClaim(claimType.ToLowerInvariant(), claimValue);
        var mismatchedValue = identity.HasClaim(claimType, claimValue.ToLowerInvariant());

        // Assert
        Expect(matchingClaim).To.Be.True();
        Expect(mismatchedType).To.Be.False();
        Expect(mismatchedValue).To.Be.False();
    }

    [Test]
    public void Anonymous_ReturnsCanonicalIdentityAndAuthenticationContext()
    {
        // Arrange
        var identity = Identity.Anonymous;
        var authentication = AuthenticationContext.Anonymous;

        // Act
        var isCanonicalIdentity = ReferenceEquals(identity, Identity.Anonymous);
        var isCanonicalAuthentication = ReferenceEquals(authentication, AuthenticationContext.Anonymous);

        // Assert
        Expect(isCanonicalIdentity).To.Be.True();
        Expect(isCanonicalAuthentication).To.Be.True();
        Expect(authentication.Identity).To.Equal(identity);
    }
}
