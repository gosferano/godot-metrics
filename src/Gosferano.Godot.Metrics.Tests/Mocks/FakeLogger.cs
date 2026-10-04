using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics.Tests.Mocks;

/// <summary>
/// Captures log messages along with their structured properties
/// </summary>
internal sealed class FakeLogger : ILogger
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, object?>();

        _entries.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception), properties));
    }
}
