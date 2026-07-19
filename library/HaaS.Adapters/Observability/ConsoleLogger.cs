using HaaS.Domain.Ports;

namespace HaaS.Adapters.Observability;

public class ConsoleLogger : ILogger
{
    private readonly TextWriter _writer;

    public ConsoleLogger()
        : this(Console.Error)
    {
    }

    public ConsoleLogger(TextWriter writer)
    {
        _writer = writer;
    }

    public void LogTrace(string message, params object?[] args)
        => WriteLine("TRACE", message, args);

    public void LogDebug(string message, params object?[] args)
        => WriteLine("DEBUG", message, args);

    public void LogInformation(string message, params object?[] args)
        => WriteLine("INFO", message, args);

    public void LogWarning(string message, params object?[] args)
        => WriteLine("WARN", message, args);

    public void LogError(Exception? exception, string message, params object?[] args)
    {
        WriteLine("ERROR", message, args);
        if (exception is not null)
            _writer.WriteLine($"  {exception.GetType().Name}: {exception.Message}");
    }

    public void LogCritical(Exception? exception, string message, params object?[] args)
    {
        WriteLine("CRITICAL", message, args);
        if (exception is not null)
            _writer.WriteLine($"  {exception.GetType().Name}: {exception.Message}");
    }

    private void WriteLine(string level, string message, params object?[] args)
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
        
        var timestamp = DateTime.UtcNow.ToString("O");
        _writer.WriteLine($"[{timestamp}] [{level}] {formatted}");
    }
}
