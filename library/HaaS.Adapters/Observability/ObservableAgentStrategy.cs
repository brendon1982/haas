using System.Diagnostics;
using HaaS.Domain.Exceptions;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

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

    public async Task<SessionResult> ExecuteAsync(AgentExecutionRequest request, ISignalPresenter presenter)
    {
        using var activity = ActivitySource.StartActivity("AgentExecute");
        activity?.SetTag("signal.source", request.Signal.Source);
        activity?.SetTag("session.id", request.SessionId);

        _logger.LogInformation("Agent execution started — session: {0}", request.SessionId);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await _inner.ExecuteAsync(request, presenter);
            sw.Stop();

            activity?.SetTag("session.id", request.SessionId);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation("Agent execution completed — session: {0}, duration: {1}ms", request.SessionId, sw.ElapsedMilliseconds);
            return result;
        }
        catch (GovernanceDeniedException)
        {
            sw.Stop();

            activity?.SetTag("governance.denied", true);
            _logger.LogWarning("Agent execution denied by governance — duration: {0}ms", sw.ElapsedMilliseconds);
            throw;
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
