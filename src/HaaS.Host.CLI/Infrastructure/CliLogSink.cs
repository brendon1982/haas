namespace HaaS.Host.CLI.Infrastructure;

using System.Collections.Concurrent;

public sealed class CliLogSink
{
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly int _maxEntries;

    public CliLogSink(int maxEntries = 100)
    {
        _maxEntries = maxEntries;
    }

    public void AddLog(string log)
    {
        _logs.Enqueue(log);
        while (_logs.Count > _maxEntries)
        {
            _logs.TryDequeue(out _);
        }
    }

    public IEnumerable<string> GetLogs() => _logs.ToArray();
}
