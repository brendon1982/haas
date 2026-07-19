namespace HaaS.Host.CLI.Infrastructure;

using Microsoft.Extensions.Logging;
using Spectre.Console;

public sealed class SpectreLoggingProvider : ILoggerProvider
{
    private readonly CliLogSink _sink;

    public SpectreLoggingProvider(CliLogSink sink)
    {
        _sink = sink;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ExternalSpectreLogger(_sink, categoryName);
    }

    public void Dispose() { }

    private sealed class ExternalSpectreLogger : ILogger
    {
        private readonly CliLogSink _sink;
        private readonly string _category;

        public ExternalSpectreLogger(CliLogSink sink, string category)
        {
            _sink = sink;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var color = logLevel switch
            {
                LogLevel.Trace => "grey",
                LogLevel.Debug => "blue",
                LogLevel.Information => "green",
                LogLevel.Warning => "yellow",
                LogLevel.Error => "red",
                LogLevel.Critical => "maroon",
                _ => "white"
            };

            var levelStr = logLevel.ToString().ToUpperInvariant();
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
            
            // Shorten category name for display
            var shortCategory = _category.Split('.').Last();
            
            var markup = $"[{color}][[{timestamp}]] [[{levelStr}]][/] [grey]{Markup.Escape(shortCategory)}:[/] {Markup.Escape(message)}";
            _sink.AddLog(markup);

            if (exception != null)
            {
                _sink.AddLog($"[{color}]  {exception.GetType().Name}: {Markup.Escape(exception.Message)}[/]");
            }
        }
    }
}
