using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IObservabilityProvider
{
    Task RecordMetricAsync(string name, double value);
    Task RecordEventAsync(AgentIterationEvent evt);
}
