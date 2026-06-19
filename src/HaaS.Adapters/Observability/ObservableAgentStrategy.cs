using System.Diagnostics;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Observability;

public sealed class ObservableAgentStrategy : IAgentStrategy
{
    private readonly IAgentStrategy _inner;
    private readonly IObservabilityProvider _telemetry;

    public ObservableAgentStrategy(IAgentStrategy inner, IObservabilityProvider telemetry)
    {
        _inner = inner;
        _telemetry = telemetry;
    }

    public async Task<SessionResult> ExecuteAsync(AgentSessionConfig config, SignalValue signal)
    {
        await _telemetry.RecordMetricAsync("agent.execute.start", 1);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await _inner.ExecuteAsync(config, signal);
            sw.Stop();

            await _telemetry.RecordMetricAsync("agent.execute.duration_ms", sw.ElapsedMilliseconds);
            await _telemetry.RecordEventAsync(new AgentIterationEvent(
                result.SessionId,
                1,
                "complete",
                signal.Payload,
                result.Output,
                DateTime.UtcNow));

            return result;
        }
        catch (Exception)
        {
            await _telemetry.RecordMetricAsync("agent.execute.error", 1);
            throw;
        }
    }
}
