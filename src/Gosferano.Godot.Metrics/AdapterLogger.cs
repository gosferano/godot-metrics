using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Fallback logger used when no logger factory is configured. Forwards warnings and errors to the engine log.
/// </summary>
internal sealed class AdapterLogger : ILogger
{
    private readonly IMonitorAdapter _adapter;

    public AdapterLogger(IMonitorAdapter adapter)
    {
        _adapter = adapter;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Warning && logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);

        if (exception is not null)
        {
            message += Environment.NewLine + exception;
        }

        _adapter.Log(logLevel, message);
    }
}
