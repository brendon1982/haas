using System.Text.Json;
using HaaS.Adapters.Observability;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ConsoleObservabilityProviderTests
{
    [Test]
    public async Task RecordMetricAsync_WritesJsonLineWithNameAndValue()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleObservabilityProvider(writer);

        // Act
        await sut.RecordMetricAsync("test.counter", 42);

        // Assert
        var line = writer.ToString().TrimEnd();
        var parsed = JsonSerializer.Deserialize<JsonElement>(line);
        Assert.Multiple(() =>
        {
            Assert.That(parsed.GetProperty("type").GetString(), Is.EqualTo("metric"));
            Assert.That(parsed.GetProperty("name").GetString(), Is.EqualTo("test.counter"));
            Assert.That(parsed.GetProperty("value").GetDouble(), Is.EqualTo(42));
        });
    }

    [Test]
    public async Task RecordEventAsync_WritesJsonLineWithEventFields()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleObservabilityProvider(writer);
        var ts = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var evt = new AgentIterationEvent("sess-1", 1, "think", "input text", null, ts);

        // Act
        await sut.RecordEventAsync(evt);

        // Assert
        var line = writer.ToString().TrimEnd();
        var parsed = JsonSerializer.Deserialize<JsonElement>(line);
        Assert.Multiple(() =>
        {
            Assert.That(parsed.GetProperty("type").GetString(), Is.EqualTo("event"));
            Assert.That(parsed.GetProperty("sessionId").GetString(), Is.EqualTo("sess-1"));
            Assert.That(parsed.GetProperty("iteration").GetInt32(), Is.EqualTo(1));
            Assert.That(parsed.GetProperty("phase").GetString(), Is.EqualTo("think"));
            Assert.That(parsed.GetProperty("input").GetString(), Is.EqualTo("input text"));
            Assert.That(parsed.GetProperty("output").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task MultipleWrites_EachOnSeparateLine()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleObservabilityProvider(writer);

        // Act
        await sut.RecordMetricAsync("m1", 1);
        await sut.RecordEventAsync(new AgentIterationEvent("s-1", 1, "decide", null, "out", DateTime.UtcNow));

        // Assert
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);
        Assert.That(lines, Has.Length.EqualTo(2));
    }
}
