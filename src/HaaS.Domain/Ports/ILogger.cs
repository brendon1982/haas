namespace HaaS.Domain.Ports;

public interface ILogger
{
    void LogTrace(string message, params object?[] args);
    void LogDebug(string message, params object?[] args);
    void LogInformation(string message, params object?[] args);
    void LogWarning(string message, params object?[] args);
    void LogError(Exception? exception, string message, params object?[] args);
    void LogCritical(Exception? exception, string message, params object?[] args);
}
