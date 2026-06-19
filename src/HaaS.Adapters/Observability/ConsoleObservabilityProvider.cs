using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Observability;

public class ConsoleObservabilityProvider : IObservabilityProvider
{
    private readonly TextWriter _writer;

    public ConsoleObservabilityProvider()
        : this(Console.Error)
    {
    }

    public ConsoleObservabilityProvider(TextWriter writer)
    {
        _writer = writer;
    }

    public Task RecordMetricAsync(string name, double value)
    {
        var entry = new
        {
            type = "metric",
            name,
            value,
            timestamp = DateTime.UtcNow
        };
        _writer.WriteLine(JsonSerializer.Serialize(entry));
        return Task.CompletedTask;
    }

    public Task RecordEventAsync(AgentIterationEvent evt)
    {
        var entry = new
        {
            type = "event",
            sessionId = evt.SessionId,
            iteration = evt.Iteration,
            phase = evt.Phase,
            input = evt.Input,
            output = evt.Output,
            timestamp = evt.Timestamp
        };
        _writer.WriteLine(JsonSerializer.Serialize(entry));
        return Task.CompletedTask;
    }
}
