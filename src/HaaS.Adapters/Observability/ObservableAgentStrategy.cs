using System.Diagnostics;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Observability;

public sealed class ObservableAgentStrategy : IAgentStrategy
{
    private static readonly ActivitySource ActivitySource = new("HaaS.Agents");

    private readonly IAgentStrategy _inner;
    private readonly ILogger _logger;

    public ObservableAgentStrategy(IAgentStrategy inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<SessionResult> ExecuteAsync(SignalValue signal, string sessionId, ISignalPresenter presenter)
    {
        using var activity = ActivitySource.StartActivity("AgentExecute");
        activity?.SetTag("signal.source", signal.Source);
        activity?.SetTag("session.id", sessionId);

        _logger.LogInformation("Agent execution started — session: {0}", sessionId);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await _inner.ExecuteAsync(signal, sessionId, presenter);
            sw.Stop();

            activity?.SetTag("session.id", result.SessionId);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation("Agent execution completed — session: {0}, duration: {1}ms", result.SessionId, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);

            _logger.LogError(ex, "Agent execution failed — duration: {0}ms", sw.ElapsedMilliseconds);

            throw;
        }
    }
}
