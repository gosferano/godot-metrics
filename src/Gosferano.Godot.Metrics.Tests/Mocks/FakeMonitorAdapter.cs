using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics.Tests.Mocks;

/// <summary>
/// Records monitor registrations instead of calling Godot. Posted actions are queued until <see cref="RunPosted"/>.
/// </summary>
internal sealed class FakeMonitorAdapter : IMonitorAdapter
{
    private readonly ConcurrentDictionary<string, MonitorDefinition> _monitors = new();
    private readonly ConcurrentQueue<string> _removed = new();
    private readonly ConcurrentQueue<string> _duplicates = new();
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _logs = new();
    private readonly ConcurrentQueue<Action> _posted = new();

    public IReadOnlyDictionary<string, MonitorDefinition> Monitors => _monitors;

    public IReadOnlyCollection<string> Removed => _removed;

    public IReadOnlyCollection<string> DuplicateAdds => _duplicates;

    public IReadOnlyCollection<(LogLevel Level, string Message)> Logs => _logs;

    public int PostedCount => _posted.Count;

    public void AddMonitor(MonitorDefinition monitor)
    {
        if (!_monitors.TryAdd(monitor.Id, monitor))
        {
            _duplicates.Enqueue(monitor.Id);
        }
    }

    public void RemoveMonitor(string id)
    {
        _monitors.TryRemove(id, out _);
        _removed.Enqueue(id);
    }

    public void Post(Action action)
    {
        _posted.Enqueue(action);
    }

    public void Log(LogLevel level, string message)
    {
        _logs.Enqueue((level, message));
    }

    /// <summary>
    /// Simulates the next main-thread frame
    /// </summary>
    public void RunPosted()
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }

    /// <summary>
    /// Simulates Godot polling a monitor
    /// </summary>
    public double Read(string id)
    {
        return _monitors[id].Read();
    }
}
