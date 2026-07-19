using System.Diagnostics;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Observability;

public sealed class ObservableRunSessionUseCase : IRunSessionUseCase
{
    private static readonly ActivitySource ActivitySource = new("HaaS.Application");

    private readonly IRunSessionUseCase _inner;
    private readonly ILogger _logger;

    public ObservableRunSessionUseCase(IRunSessionUseCase inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<SessionResult> ExecuteAsync(Signal signal, ISignalPresenter presenter)
    {
        using var activity = ActivitySource.StartActivity("RunSession");
        activity?.SetTag("signal.source", signal.Source);
        
        _logger.LogInformation("Session processing started from source: {0}", signal.Source);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await _inner.ExecuteAsync(signal, presenter);
            sw.Stop();

            activity?.SetTag("session.id", result.SessionId);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation("Session processing completed — session: {0}, duration: {1}ms", result.SessionId, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetTag("error", true);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex, "Session processing failed — duration: {0}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }
}
