using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record PolicyConditions
{
    public ImmutableArray<string> SourceTypes { get; }
    public ImmutableArray<PolicySubject> Subjects { get; }
    public ImmutableArray<string> Roles { get; }
    public ImmutableArray<ClaimCondition> Claims { get; }
    public ImmutableArray<AttributeCondition> Attributes { get; }
    public ImmutableArray<string> ToolNames { get; }
    public ImmutableArray<UtcTimeWindow> TimeWindows { get; }

    public PolicyConditions(
        IEnumerable<string>? sourceTypes = null,
        IEnumerable<PolicySubject>? subjects = null,
        IEnumerable<string>? roles = null,
        IEnumerable<ClaimCondition>? claims = null,
        IEnumerable<AttributeCondition>? attributes = null,
        IEnumerable<string>? toolNames = null,
        IEnumerable<UtcTimeWindow>? timeWindows = null)
    {
        SourceTypes = PolicyValidation.NormalizeStrings(sourceTypes, nameof(sourceTypes));
        Subjects = PolicyValidation.NormalizeRecords(
            subjects,
            subject => $"{subject.Issuer}\0{subject.Subject}",
            nameof(subjects));
        Roles = PolicyValidation.NormalizeStrings(roles, nameof(roles));
        Claims = PolicyValidation.NormalizeRecords(
            claims,
            claim => $"{claim.ClaimType}\0{claim.Operator}\0{string.Join("\0", claim.Values)}",
            nameof(claims));
        Attributes = PolicyValidation.NormalizeRecords(
            attributes,
            attribute => $"{attribute.AttributeName}\0{attribute.Operator}\0{string.Join("\0", attribute.Values)}",
            nameof(attributes));
        ToolNames = PolicyValidation.NormalizeStrings(toolNames, nameof(toolNames));
        TimeWindows = PolicyValidation.NormalizeRecords(
            timeWindows,
            window => $"{string.Join(",", window.Days.Order())}\0{window.Start:O}\0{window.End:O}",
            nameof(timeWindows));
    }

    public bool IsEmpty => SourceTypes.IsEmpty
        && Subjects.IsEmpty
        && Roles.IsEmpty
        && Claims.IsEmpty
        && Attributes.IsEmpty
        && ToolNames.IsEmpty
        && TimeWindows.IsEmpty;
}
