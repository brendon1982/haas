namespace HaaS.Host.CLI.Infrastructure;

using HaaS.Domain.Ports;

public sealed class GuiLogger : ILogger
{
    private readonly GuiLayoutManager _layoutManager;

    public GuiLogger(GuiLayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
    }

    public void LogTrace(string message, params object?[] args) => Log("TRACE", message, args);
    public void LogDebug(string message, params object?[] args) => Log("DEBUG", message, args);
    public void LogInformation(string message, params object?[] args) => Log("INFO", message, args);
    public void LogWarning(string message, params object?[] args) => Log("WARN", message, args);

    public void LogError(Exception? exception, string message, params object?[] args)
    {
        Log("ERROR", message, args);
        if (exception != null)
        {
            _layoutManager.AddLog($"  {exception.GetType().Name}: {exception.Message}");
        }
    }

    public void LogCritical(Exception? exception, string message, params object?[] args)
    {
        Log("CRITICAL", message, args);
        if (exception != null)
        {
            _layoutManager.AddLog($"  {exception.GetType().Name}: {exception.Message}");
        }
    }

    private void Log(string level, string message, params object?[] args)
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
        var log = $"[{timestamp}] [{level}] {formatted}";
        _layoutManager.AddLog(log);
    }
}
