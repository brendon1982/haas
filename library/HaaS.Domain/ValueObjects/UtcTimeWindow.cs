using System.Collections.Immutable;

namespace HaaS.Domain.ValueObjects;

public sealed record UtcTimeWindow
{
    public ImmutableHashSet<DayOfWeek> Days { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public UtcTimeWindow(IEnumerable<DayOfWeek> days, TimeOnly start, TimeOnly end)
    {
        ArgumentNullException.ThrowIfNull(days);

        Days = days
            .Distinct()
            .Order()
            .ToImmutableHashSet();

        if (Days.Count == 0 || Days.Any(day => !Enum.IsDefined(day)))
        {
            throw new ArgumentException(
                "A UTC time window must contain at least one valid day.",
                nameof(days));
        }

        if (start == end)
        {
            throw new ArgumentException(
                "A UTC time window start and end must differ.",
                nameof(end));
        }

        Start = start;
        End = end;
    }
}
