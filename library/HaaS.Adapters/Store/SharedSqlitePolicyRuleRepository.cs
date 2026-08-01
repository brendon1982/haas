using System.Globalization;
using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace HaaS.Adapters.Store;

public sealed class SharedSqlitePolicyRuleRepository : IPolicyRuleRepository
{
    private static readonly string[] ConditionPropertyNames =
    [
        "sourceTypes",
        "subjects",
        "roles",
        "claims",
        "attributes",
        "toolNames",
        "timeWindows"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _connectionString;

    public SharedSqlitePolicyRuleRepository(
        string databasePath,
        IEnumerable<PolicyRule>? seedRules = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();

        InitializeDatabase();
        InsertSeedsIfAbsent(seedRules);
    }

    public async Task<PolicyRule?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText += " WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRule(reader)
            : null;
    }

    public async Task<IReadOnlyList<PolicyRule>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText += " ORDER BY id COLLATE BINARY;";

        return await ReadRulesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<PolicyRule>> GetByGateAsync(
        PolicyGate gate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireDefined(gate, nameof(gate));

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText += " WHERE gate = $gate ORDER BY id COLLATE BINARY;";
        command.Parameters.AddWithValue("$gate", gate.ToString());

        return await ReadRulesAsync(command, cancellationToken);
    }

    public async Task SaveAsync(
        PolicyRule rule,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(rule);
        PolicyRuleValidator.Validate(rule);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO policy_rules (
                id, gate, priority, effect, conditions_json, created_at, updated_at
            ) VALUES (
                $id, $gate, $priority, $effect, $conditions, $created, $updated
            ) ON CONFLICT(id) DO UPDATE SET
                gate = excluded.gate,
                priority = excluded.priority,
                effect = excluded.effect,
                conditions_json = excluded.conditions_json,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at;
            """;
        AddRuleParameters(command, rule);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM policy_rules WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS policy_rules (
                id TEXT PRIMARY KEY,
                gate TEXT NOT NULL,
                priority INTEGER NOT NULL,
                effect TEXT NOT NULL,
                conditions_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private void InsertSeedsIfAbsent(IEnumerable<PolicyRule>? seedRules)
    {
        if (seedRules is null)
        {
            return;
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        foreach (var rule in seedRules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            PolicyRuleValidator.Validate(rule);

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO policy_rules (
                    id, gate, priority, effect, conditions_json, created_at, updated_at
                ) VALUES (
                    $id, $gate, $priority, $effect, $conditions, $created, $updated
                ) ON CONFLICT(id) DO NOTHING;
                """;
            AddRuleParameters(command, rule);
            command.ExecuteNonQuery();
        }
    }

    private static SqliteCommand CreateSelectCommand(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, gate, priority, effect, conditions_json, created_at, updated_at
            FROM policy_rules
            """;
        return command;
    }

    private static async Task<IReadOnlyList<PolicyRule>> ReadRulesAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var rules = new List<PolicyRule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(ReadRule(reader));
        }

        return rules;
    }

    private static PolicyRule ReadRule(SqliteDataReader reader)
    {
        var id = reader.GetString(0);
        try
        {
            return new PolicyRule(
                id,
                ParseEnum<PolicyGate>(reader.GetString(1), "gate"),
                reader.GetInt32(2),
                ParseEnum<PolicyEffect>(reader.GetString(3), "effect"),
                DeserializeConditions(reader.GetString(4)),
                ParseTimestamp(reader.GetString(5), "created_at"),
                ParseTimestamp(reader.GetString(6), "updated_at"));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or FormatException
                or JsonException
                or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Persisted policy rule '{id}' contains invalid data.",
                exception);
        }
    }

    private static void AddRuleParameters(SqliteCommand command, PolicyRule rule)
    {
        command.Parameters.AddWithValue("$id", rule.Id);
        command.Parameters.AddWithValue("$gate", rule.Gate.ToString());
        command.Parameters.AddWithValue("$priority", rule.Priority);
        command.Parameters.AddWithValue("$effect", rule.Effect.ToString());
        command.Parameters.AddWithValue("$conditions", SerializeConditions(rule.Conditions));
        command.Parameters.AddWithValue(
            "$created",
            rule.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "$updated",
            rule.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static string SerializeConditions(PolicyConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        var document = new ConditionDocument(
            [.. conditions.SourceTypes],
            conditions.Subjects
                .Select(subject => new SubjectDocument(subject.Issuer, subject.Subject))
                .ToArray(),
            [.. conditions.Roles],
            conditions.Claims
                .Select(claim => new ClaimDocument(
                    claim.ClaimType,
                    claim.Operator.ToString(),
                    [.. claim.Values]))
                .ToArray(),
            conditions.Attributes
                .Select(attribute => new AttributeDocument(
                    attribute.AttributeName,
                    attribute.Operator.ToString(),
                    [.. attribute.Values]))
                .ToArray(),
            [.. conditions.ToolNames],
            conditions.TimeWindows
                .Select(window => new TimeWindowDocument(
                    window.Days
                        .Order()
                        .Select(day => day.ToString())
                        .ToArray(),
                    window.Start.ToString("O", CultureInfo.InvariantCulture),
                    window.End.ToString("O", CultureInfo.InvariantCulture)))
                .ToArray());

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static PolicyConditions DeserializeConditions(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var properties = ReadRequiredProperties(
                document.RootElement,
                "conditions",
                ConditionPropertyNames);

            return new PolicyConditions(
                ReadStringArray(properties["sourceTypes"], "sourceTypes"),
                ReadSubjects(properties["subjects"]),
                ReadStringArray(properties["roles"], "roles"),
                ReadClaims(properties["claims"]),
                ReadAttributes(properties["attributes"]),
                ReadStringArray(properties["toolNames"], "toolNames"),
                ReadTimeWindows(properties["timeWindows"]));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or FormatException
                or JsonException
                or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Policy rule conditions JSON must contain only valid typed conditions.",
                exception);
        }
    }

    private static IEnumerable<PolicySubject> ReadSubjects(JsonElement element)
    {
        return ReadArray(element, "subjects")
            .Select(subject =>
            {
                var properties = ReadRequiredProperties(
                    subject,
                    "subject",
                    ["issuer", "subject"]);
                return new PolicySubject(
                    ReadString(properties["issuer"], "subject.issuer"),
                    ReadString(properties["subject"], "subject.subject"));
            })
            .ToArray();
    }

    private static IEnumerable<ClaimCondition> ReadClaims(JsonElement element)
    {
        return ReadArray(element, "claims")
            .Select(claim =>
            {
                var properties = ReadRequiredProperties(
                    claim,
                    "claim",
                    ["claimType", "operator", "values"]);
                return new ClaimCondition(
                    ReadString(properties["claimType"], "claim.claimType"),
                    ReadEnum<ClaimMatchOperator>(properties["operator"], "claim.operator"),
                    ReadStringArray(properties["values"], "claim.values"));
            })
            .ToArray();
    }

    private static IEnumerable<AttributeCondition> ReadAttributes(JsonElement element)
    {
        return ReadArray(element, "attributes")
            .Select(attribute =>
            {
                var properties = ReadRequiredProperties(
                    attribute,
                    "attribute",
                    ["attributeName", "operator", "values"]);
                return new AttributeCondition(
                    ReadString(properties["attributeName"], "attribute.attributeName"),
                    ReadEnum<AttributeMatchOperator>(
                        properties["operator"],
                        "attribute.operator"),
                    ReadStringArray(properties["values"], "attribute.values"));
            })
            .ToArray();
    }

    private static IEnumerable<UtcTimeWindow> ReadTimeWindows(JsonElement element)
    {
        return ReadArray(element, "timeWindows")
            .Select(window =>
            {
                var properties = ReadRequiredProperties(
                    window,
                    "time window",
                    ["days", "start", "end"]);
                return new UtcTimeWindow(
                    ReadStringArray(properties["days"], "timeWindow.days")
                        .Select(day => ParseEnum<DayOfWeek>(day, "timeWindow.days")),
                    ParseTime(properties["start"], "timeWindow.start"),
                    ParseTime(properties["end"], "timeWindow.end"));
            })
            .ToArray();
    }

    private static IEnumerable<string> ReadStringArray(JsonElement element, string context)
        => ReadArray(element, context)
            .Select(item => ReadString(item, context))
            .ToArray();

    private static IEnumerable<JsonElement> ReadArray(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{context} must be a JSON array.");
        }

        return element.EnumerateArray().ToArray();
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadRequiredProperties(
        JsonElement element,
        string context,
        IEnumerable<string> expectedPropertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{context} must be a JSON object.");
        }

        var expected = new HashSet<string>(expectedPropertyNames, StringComparer.Ordinal);
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name))
            {
                throw new InvalidOperationException(
                    $"{context} contains unsupported property '{property.Name}'.");
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new InvalidOperationException(
                    $"{context} contains duplicate property '{property.Name}'.");
            }
        }

        if (properties.Count != expected.Count || expected.Any(name => !properties.ContainsKey(name)))
        {
            throw new InvalidOperationException($"{context} does not contain all required properties.");
        }

        return properties;
    }

    private static string ReadString(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } value)
        {
            throw new InvalidOperationException($"{context} must be a JSON string.");
        }

        return value;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string context)
        where TEnum : struct, Enum
        => ParseEnum<TEnum>(ReadString(element, context), context);

    private static TEnum ParseEnum<TEnum>(string value, string context)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed)
            || !Enum.IsDefined(parsed)
            || !StringComparer.Ordinal.Equals(value, Enum.GetName(parsed)))
        {
            throw new InvalidOperationException($"{context} contains an unsupported enum value.");
        }

        return parsed;
    }

    private static TimeOnly ParseTime(JsonElement element, string context)
    {
        var value = ReadString(element, context);
        if (!TimeOnly.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            throw new FormatException($"{context} must use the round-trip time format.");
        }

        return time;
    }

    private static DateTimeOffset ParseTimestamp(string value, string columnName)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            throw new FormatException($"{columnName} must use the round-trip timestamp format.");
        }

        return timestamp;
    }

    private static void RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unsupported enum value.");
        }
    }

    private sealed record ConditionDocument(
        string[] SourceTypes,
        SubjectDocument[] Subjects,
        string[] Roles,
        ClaimDocument[] Claims,
        AttributeDocument[] Attributes,
        string[] ToolNames,
        TimeWindowDocument[] TimeWindows);

    private sealed record SubjectDocument(string Issuer, string Subject);

    private sealed record ClaimDocument(string ClaimType, string Operator, string[] Values);

    private sealed record AttributeDocument(
        string AttributeName,
        string Operator,
        string[] Values);

    private sealed record TimeWindowDocument(string[] Days, string Start, string End);
}
