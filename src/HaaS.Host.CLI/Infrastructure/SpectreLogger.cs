namespace HaaS.Host.CLI.Infrastructure;

using HaaS.Domain.Ports;
using Spectre.Console;

public sealed class SpectreLogger : ILogger
{
    private readonly CliLogSink _sink;

    public SpectreLogger(CliLogSink sink)
    {
        _sink = sink;
    }

    public void LogTrace(string message, params object?[] args) => Log("grey", "TRACE", message, args);
    public void LogDebug(string message, params object?[] args) => Log("blue", "DEBUG", message, args);
    public void LogInformation(string message, params object?[] args) => Log("green", "INFO", message, args);
    public void LogWarning(string message, params object?[] args) => Log("yellow", "WARN", message, args);

    public void LogError(Exception? exception, string message, params object?[] args)
    {
        Log("red", "ERROR", message, args);
        if (exception != null)
        {
            _sink.AddLog($"[red]  {exception.GetType().Name}: {Markup.Escape(exception.Message)}[/]");
        }
    }

    public void LogCritical(Exception? exception, string message, params object?[] args)
    {
        Log("maroon", "CRITICAL", message, args);
        if (exception != null)
        {
            _sink.AddLog($"[maroon]  {exception.GetType().Name}: {Markup.Escape(exception.Message)}[/]");
        }
    }

    private void Log(string color, string level, string message, params object?[] args)
    {
        string formatted;
        try
        {
            formatted = args.Length > 0 ? string.Format(null, message, args) : message;
        }
        catch (FormatException)
        {
            formatted = $"{message} (Args: {string.Join(", ", args)})";
        }

        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
        var markup = $"[{color}][[{timestamp}]] [[{level}]][/] {Markup.Escape(formatted)}";
        _sink.AddLog(markup);
    }
}
