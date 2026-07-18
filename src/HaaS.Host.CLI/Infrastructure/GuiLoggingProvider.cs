namespace HaaS.Host.CLI.Infrastructure;

using Microsoft.Extensions.Logging;

public sealed class GuiLoggingProvider : ILoggerProvider
{
    private readonly GuiLayoutManager _layoutManager;

    public GuiLoggingProvider(GuiLayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ExternalGuiLogger(_layoutManager, categoryName);
    }

    public void Dispose() { }

    private sealed class ExternalGuiLogger : ILogger
    {
        private readonly GuiLayoutManager _layoutManager;
        private readonly string _category;

        public ExternalGuiLogger(GuiLayoutManager layoutManager, string category)
        {
            _layoutManager = layoutManager;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var levelStr = logLevel.ToString().ToUpperInvariant();
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
            var shortCategory = _category.Split('.').Last();

            var log = $"[{timestamp}] [{levelStr}] {shortCategory}: {message}";
            _layoutManager.AddLog(log);

            if (exception != null)
            {
                _layoutManager.AddLog($"  {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
