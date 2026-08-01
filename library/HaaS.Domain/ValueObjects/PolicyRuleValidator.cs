using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public static class PolicyRuleValidator
{
    public static void Validate(PolicyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        PolicyValidation.RequireNonEmpty(rule.Id, nameof(rule.Id));
        PolicyValidation.RequireDefined(rule.Gate, nameof(rule.Gate));
        PolicyValidation.RequireDefined(rule.Effect, nameof(rule.Effect));
        ArgumentNullException.ThrowIfNull(rule.Conditions);

        if (rule.Gate == PolicyGate.SessionStart && !rule.Conditions.ToolNames.IsEmpty)
        {
            throw new ArgumentException(
                "SessionStart rules cannot contain tool conditions.",
                nameof(rule));
        }
    }
}

internal static class PolicyValidation
{
    public static string RequireNonEmpty(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    public static void RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unsupported enum value.");
        }
    }

    public static ImmutableArray<string> NormalizeStrings(
        IEnumerable<string>? values,
        string parameterName)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => RequireNonEmpty(value, parameterName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public static ImmutableArray<T> NormalizeRecords<T>(
        IEnumerable<T>? values,
        Func<T, string> sortKey,
        string parameterName)
        where T : class
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => value ?? throw new ArgumentException("Condition entries cannot be null.", parameterName))
            .Distinct()
            .OrderBy(sortKey, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public static void ValidateValueShape(
        bool requiresValues,
        ImmutableArray<string> values,
        string parameterName)
    {
        if (requiresValues && values.IsEmpty)
        {
            throw new ArgumentException("This operator requires one or more values.", parameterName);
        }

        if (!requiresValues && !values.IsEmpty)
        {
            throw new ArgumentException("This operator does not accept values.", parameterName);
        }
    }
}
